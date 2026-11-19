"""LumaDesk account and CAS synchronization service. Python 3.11+."""
from __future__ import annotations
import base64
import hashlib
import hmac
import json
import os
import re
import secrets
import sqlite3
import threading
import time
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path
from typing import Annotated, Literal
from fastapi import FastAPI, Depends, HTTPException, Request, Response
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

os.umask(0o077)
DB_PATH = Path(os.getenv('LUMADESK_DB', './data/lumadesk.sqlite3'))
REGISTRATION = os.getenv('LUMADESK_REGISTRATION', 'true').lower() == 'true'
SESSION_SECONDS = int(os.getenv('LUMADESK_SESSION_DAYS', '30')) * 86400
ITERATIONS = 600_000
BODY_LIMIT = 1024 * 1024
HASH_SLOTS = threading.BoundedSemaphore(2)

class Strict(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True, allow_inf_nan=False)

class Credentials(Strict):
    username: str = Field(min_length=3, max_length=32, pattern=r'^[A-Za-z0-9_.-]+$')
    password: str = Field(min_length=12, max_length=128)
    device_id: str = Field(min_length=1, max_length=64, pattern=r'^[A-Za-z0-9_-]+$')

class LocationData(Strict):
    City: str = Field(default='Москва', min_length=1, max_length=100)
    Lat: float = Field(default=55.7522, ge=-90, le=90)
    Lon: float = Field(default=37.6156, ge=-180, le=180)

class Settings(Strict):
    Seconds: bool = True
    Sound: bool = True
    Snapping: bool = True
    Opacity: int = Field(default=84, ge=40, le=100)
    Refresh: int = Field(default=30, ge=15, le=180)
    WeatherMode: int = Field(default=0, ge=0, le=2)
    Location: LocationData = Field(default_factory=LocationData)

class Event(Strict):
    Id: str = Field(min_length=1, max_length=64, pattern=r'^[A-Za-z0-9_-]+$')
    Title: str = Field(min_length=1, max_length=140)
    Kind: Literal['alarm', 'reminder']
    Sound: Literal['@dawn', '@orbit', '@bells'] = '@dawn'
    NextLocal: str
    Days: int = Field(ge=0, le=127)
    Enabled: bool
    Snooze: int = Field(ge=1, le=120)

    @field_validator('NextLocal')
    @classmethod
    def validate_date(cls, value):
        if not re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}', value):
            raise ValueError('Expected local ISO date without timezone')
        datetime.strptime(value, '%Y-%m-%dT%H:%M:%S')
        return value

class NoteData(Strict):
    Id: str = Field(min_length=1, max_length=64, pattern=r'^[A-Za-z0-9_-]+$')
    Title: str = Field(min_length=1, max_length=140)
    Body: str = Field(default='', max_length=20000)
    UpdatedUtc: str

    @field_validator('UpdatedUtc')
    @classmethod
    def utc_timestamp(cls, value):
        if not value.endswith('Z'):
            raise ValueError('UTC timestamp required')
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
        if parsed.tzinfo is None:
            raise ValueError('UTC timestamp required')
        return value

class Document(Strict):
    schema_version: Literal[2] = Field(default=2, alias='schema')
    items: list[Event] = Field(default_factory=list, max_length=1000)
    notes: list[NoteData] = Field(default_factory=list, max_length=500)
    settings: Settings = Field(default_factory=Settings)

    @model_validator(mode='after')
    def unique_ids(self):
        if len({event.Id for event in self.items}) != len(self.items):
            raise ValueError('Duplicate event IDs')
        if len({note.Id for note in self.notes}) != len(self.notes):
            raise ValueError('Duplicate note IDs')
        if sum(len(note.Body) for note in self.notes) > 200000:
            raise ValueError('Total notes text exceeds quota')
        if len(json.dumps(self.model_dump(by_alias=True), ensure_ascii=False, separators=(',', ':')).encode('utf-8')) > 900000:
            raise ValueError('Document exceeds synchronization quota')
        return self

class WriteSync(Strict):
    expected_revision: int = Field(ge=0)
    document: Document

class ChangePassword(Strict):
    old_password: str = Field(min_length=12, max_length=128)
    new_password: str = Field(min_length=12, max_length=128)

class DeleteAccount(Strict):
    password: str = Field(min_length=12, max_length=128)

class LimitedBody:
    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope['type'] != 'http':
            return await self.app(scope, receive, send)
        length = dict(scope.get('headers', [])).get(b'content-length', b'0')
        try:
            too_large = int(length) > BODY_LIMIT
        except ValueError:
            too_large = True
        if too_large:
            return await JSONResponse({'detail': 'Слишком большой запрос.'}, status_code=413)(scope, receive, send)
        body = bytearray()
        while True:
            message = await receive()
            if message['type'] == 'http.disconnect':
                return
            body.extend(message.get('body', b''))
            if len(body) > BODY_LIMIT:
                return await JSONResponse({'detail': 'Слишком большой запрос.'}, status_code=413)(scope, receive, send)
            if not message.get('more_body', False):
                break
        delivered = False
        async def replay():
            nonlocal delivered
            if not delivered:
                delivered = True
                return {'type': 'http.request', 'body': bytes(body), 'more_body': False}
            return await receive()
        async def secure_send(message):
            if message['type'] == 'http.response.start':
                message.setdefault('headers', []).extend([
                    (b'cache-control', b'no-store'),
                    (b'x-content-type-options', b'nosniff'),
                ])
            await send(message)
        await self.app(scope, replay, secure_send)

app = FastAPI(title='LumaDesk Sync', version='2.1', docs_url=None, redoc_url=None)
app.add_middleware(LimitedBody)

@app.exception_handler(RequestValidationError)
async def invalid_request(request, error):
    # Pydantic's default response can include the input, including passwords.
    return JSONResponse({'detail': 'Некорректные данные запроса.'}, status_code=422)


@contextmanager
def db():
    connection = sqlite3.connect(DB_PATH, timeout=10)
    connection.row_factory = sqlite3.Row
    connection.execute('PRAGMA foreign_keys=ON')
    try:
        yield connection
    finally:
        connection.close()

def initialize():
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    with db() as connection:
        connection.execute('PRAGMA journal_mode=WAL')
        connection.executescript('''
        CREATE TABLE IF NOT EXISTS users (
          id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL UNIQUE,
          password_hash TEXT NOT NULL, created_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS sessions (
          token_hash TEXT PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
          device_id TEXT NOT NULL, created_at INTEGER NOT NULL, expires_at INTEGER NOT NULL);
        CREATE INDEX IF NOT EXISTS sessions_user ON sessions(user_id);
        CREATE TABLE IF NOT EXISTS documents (
          user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
          revision INTEGER NOT NULL, body TEXT NOT NULL, updated_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS limits (
          key TEXT PRIMARY KEY, started INTEGER NOT NULL, count INTEGER NOT NULL);
        ''')
        connection.execute('BEGIN IMMEDIATE')
        for row in connection.execute('SELECT * FROM documents').fetchall():
            payload = json.loads(row['body'])
            if payload.get('schema', 1) == 1:
                payload['schema'] = 2
                payload.setdefault('notes', [])
                migrated = Document.model_validate(payload).model_dump_json(by_alias=True)
                connection.execute('UPDATE documents SET body=?, revision=revision+1 WHERE user_id=?', (migrated, row['user_id']))
            else:
                Document.model_validate(payload)
        connection.commit()

initialize()

def encode_password(password):
    salt = secrets.token_bytes(16)
    with HASH_SLOTS:
        digest = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt, ITERATIONS)
    return f'pbkdf2-sha256${ITERATIONS}${salt.hex()}${digest.hex()}'

DUMMY_HASH = encode_password('LumaDesk dummy password for timing')

def verify_password(password, encoded):
    try:
        algorithm, iterations, salt, expected = encoded.split('$')
        if algorithm != 'pbkdf2-sha256' or int(iterations) != ITERATIONS:
            return False
        with HASH_SLOTS:
            digest = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), bytes.fromhex(salt), int(iterations))
        return hmac.compare_digest(digest.hex(), expected)
    except (ValueError, TypeError):
        return False

def limit(key, maximum, seconds):
    now = int(time.time())
    key = hashlib.sha256(key.encode()).hexdigest()
    with db() as connection:
        connection.execute('BEGIN IMMEDIATE')
        connection.execute('DELETE FROM limits WHERE started < ?', (now - 86400,))
        row = connection.execute('SELECT * FROM limits WHERE key=?', (key,)).fetchone()
        if row is None or row['started'] + seconds <= now:
            connection.execute('INSERT OR REPLACE INTO limits VALUES (?,?,?)', (key, now, 1))
        elif row['count'] >= maximum:
            connection.rollback()
            raise HTTPException(429, 'Слишком много попыток. Попробуйте позже.', headers={'Retry-After': str(max(1, row['started'] + seconds - now))})
        else:
            connection.execute('UPDATE limits SET count=count+1 WHERE key=?', (key,))
        connection.commit()

def ip(request):
    return request.client.host if request.client else 'unknown'

def token_hash(token):
    return hashlib.sha256(token.encode('ascii', errors='strict')).hexdigest()

def issue_session(connection, user, device_id):
    now = int(time.time())
    token = secrets.token_urlsafe(32)
    connection.execute('DELETE FROM sessions WHERE expires_at<=? OR (user_id=? AND device_id=?)', (now, user['id'], device_id))
    connection.execute('INSERT INTO sessions VALUES (?,?,?,?,?)', (token_hash(token), user['id'], device_id, now, now + SESSION_SECONDS))
    connection.execute('DELETE FROM sessions WHERE user_id=? AND token_hash NOT IN (SELECT token_hash FROM sessions WHERE user_id=? ORDER BY created_at DESC, rowid DESC LIMIT 10)', (user['id'], user['id']))
    return {'token': token, 'user_id': user['id'], 'username': user['username'], 'expires_at': now + SESSION_SECONDS}

def authenticated(request: Request):
    auth = request.headers.get('authorization', '')
    if not auth.startswith('Bearer ') or not 30 <= len(auth[7:]) <= 100:
        raise HTTPException(401, 'Войдите в аккаунт.')
    try:
        digest = token_hash(auth[7:])
    except UnicodeEncodeError:
        raise HTTPException(401, 'Недействительный сеанс.')
    with db() as connection:
        row = connection.execute('SELECT sessions.*, users.username FROM sessions JOIN users ON users.id=sessions.user_id WHERE token_hash=? AND expires_at>?', (digest, int(time.time()))).fetchone()
    if row is None:
        raise HTTPException(401, 'Сеанс истёк. Войдите снова.')
    limit('api:' + str(row['user_id']), 240, 60)
    return dict(row)

User = Annotated[dict, Depends(authenticated)]

def snapshot(row):
    return {'revision': row['revision'], 'document': json.loads(row['body']), 'updated_at': row['updated_at']}

@app.get('/health')
def health():
    return {'status': 'ok', 'version': '2.1'}

@app.post('/v1/auth/register', status_code=201)
def register(payload: Credentials, request: Request):
    if not REGISTRATION:
        raise HTTPException(403, 'Регистрация отключена администратором.')
    limit('register:' + ip(request), 5, 3600)
    username = payload.username.lower()
    encoded = encode_password(payload.password)
    now = int(time.time())
    with db() as connection:
        connection.execute('BEGIN IMMEDIATE')
        if connection.execute('SELECT count(*) FROM users').fetchone()[0] >= int(os.getenv('LUMADESK_MAX_USERS', '1000')):
            connection.rollback()
            raise HTTPException(503, 'Регистрация временно недоступна.')
        try:
            cursor = connection.execute('INSERT INTO users(username,password_hash,created_at) VALUES (?,?,?)', (username, encoded, now))
        except sqlite3.IntegrityError:
            connection.rollback()
            raise HTTPException(409, 'Этот логин уже занят.')
        user = {'id': cursor.lastrowid, 'username': username}
        connection.execute('INSERT INTO documents VALUES (?,?,?,?)', (user['id'], 0, Document().model_dump_json(by_alias=True), now))
        result = issue_session(connection, user, payload.device_id)
        connection.commit()
        return result

@app.post('/v1/auth/login')
def login(payload: Credentials, request: Request):
    username = payload.username.lower()
    limit('login-ip:' + ip(request), 60, 900)
    limit('login-account:' + username, 10, 900)
    with db() as connection:
        user = connection.execute('SELECT * FROM users WHERE username=?', (username,)).fetchone()
        encoded = user['password_hash'] if user else DUMMY_HASH
        valid = verify_password(payload.password, encoded)
        if not user or not valid:
            raise HTTPException(401, 'Неверный логин или пароль.')
        connection.execute('BEGIN IMMEDIATE')
        latest = connection.execute('SELECT password_hash FROM users WHERE id=?', (user['id'],)).fetchone()
        if latest is None or latest['password_hash'] != encoded:
            connection.rollback()
            raise HTTPException(401, 'Неверный логин или пароль.')
        result = issue_session(connection, user, payload.device_id)
        connection.commit()
        return result

@app.get('/v1/auth/me')
def me(user: User):
    return {'user_id': user['user_id'], 'username': user['username'], 'expires_at': user['expires_at']}

@app.post('/v1/auth/logout', status_code=204)
def logout(user: User):
    with db() as connection:
        connection.execute('DELETE FROM sessions WHERE token_hash=?', (user['token_hash'],))
        connection.commit()
    return Response(status_code=204)

@app.post('/v1/auth/password')
def password(payload: ChangePassword, user: User):
    limit('password:' + str(user['user_id']), 5, 900)
    with db() as connection:
        row = connection.execute('SELECT * FROM users WHERE id=?', (user['user_id'],)).fetchone()
        if not verify_password(payload.old_password, row['password_hash']):
            raise HTTPException(401, 'Неверный текущий пароль.')
        encoded = encode_password(payload.new_password)
        connection.execute('BEGIN IMMEDIATE')
        # Verify again transactionally in case another device changed the password.
        current = connection.execute('SELECT password_hash FROM users WHERE id=?', (user['user_id'],)).fetchone()
        if current['password_hash'] != row['password_hash']:
            connection.rollback()
            raise HTTPException(409, 'Пароль уже изменён. Войдите заново.')
        connection.execute('UPDATE users SET password_hash=? WHERE id=?', (encoded, user['user_id']))
        connection.execute('DELETE FROM sessions WHERE user_id=?', (user['user_id'],))
        result = issue_session(connection, row, user['device_id'])
        connection.commit()
        return result

@app.get('/v1/sync')
def read_sync(user: User):
    with db() as connection:
        return snapshot(connection.execute('SELECT * FROM documents WHERE user_id=?', (user['user_id'],)).fetchone())

@app.put('/v1/sync')
def write_sync(payload: WriteSync, user: User):
    body = json.dumps(payload.document.model_dump(by_alias=True), sort_keys=True, separators=(',', ':'), ensure_ascii=False)
    now = int(time.time())
    with db() as connection:
        connection.execute('BEGIN IMMEDIATE')
        row = connection.execute('SELECT * FROM documents WHERE user_id=?', (user['user_id'],)).fetchone()
        equal = json.loads(row['body']) == json.loads(body)
        if row['revision'] != payload.expected_revision and not equal:
            connection.rollback()
            return JSONResponse({'detail': 'Данные изменены другим устройством.', **snapshot(row)}, status_code=409)
        if not equal:
            connection.execute('UPDATE documents SET revision=revision+1, body=?, updated_at=? WHERE user_id=?', (body, now, user['user_id']))
        row = connection.execute('SELECT * FROM documents WHERE user_id=?', (user['user_id'],)).fetchone()
        connection.commit()
        return snapshot(row)

@app.delete('/v1/auth/account', status_code=204)
def delete_account(payload: DeleteAccount, user: User):
    limit('delete:' + str(user['user_id']), 5, 900)
    with db() as connection:
        row = connection.execute('SELECT password_hash FROM users WHERE id=?', (user['user_id'],)).fetchone()
        if not verify_password(payload.password, row['password_hash']):
            raise HTTPException(401, 'Неверный пароль.')
        connection.execute('DELETE FROM users WHERE id=? AND password_hash=?', (user['user_id'], row['password_hash']))
        connection.commit()
    return Response(status_code=204)
