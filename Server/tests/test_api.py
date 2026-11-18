import os
import sys
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
os.environ.setdefault('LUMADESK_DB', str(Path(tempfile.gettempdir()) / 'lumadesk-test-bootstrap.sqlite3'))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fastapi.testclient import TestClient
import app

PASSWORD = 'password-for-local-tests'

def event(id='event1', title='Test'):
    return {'Id': id, 'Title': title, 'Kind': 'alarm', 'Sound': '@bells', 'NextLocal': '2027-01-02T09:30:00', 'Days': 62, 'Enabled': True, 'Snooze': 5}

def document(items=None):
    return {'schema': 2, 'items': items or [], 'notes': [], 'settings': {}}

class ApiTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        app.DB_PATH = Path(self.temp.name) / 'db.sqlite3'
        app.REGISTRATION = True
        app.initialize()
        self.client = TestClient(app.app)

    def tearDown(self):
        self.client.close()
        self.temp.cleanup()

    def register(self, username='alice', device='pc1'):
        result = self.client.post('/v1/auth/register', json={'username': username, 'password': PASSWORD, 'device_id': device})
        self.assertEqual(result.status_code, 201, result.text)
        return result.json()

    def headers(self, session):
        return {'Authorization': 'Bearer ' + session['token']}

    def login(self, username='alice', device='pc2', password=PASSWORD):
        return self.client.post('/v1/auth/login', json={'username': username, 'password': password, 'device_id': device})

    def test_registration_and_hash_storage(self):
        session = self.register('Alice')
        self.assertEqual(session['username'], 'alice')
        with app.db() as connection:
            user = connection.execute('SELECT * FROM users').fetchone()
            stored = connection.execute('SELECT * FROM sessions').fetchone()
        self.assertNotEqual(user['password_hash'], PASSWORD)
        self.assertTrue(app.verify_password(PASSWORD, user['password_hash']))
        self.assertNotEqual(stored['token_hash'], session['token'])
        self.assertEqual(len(stored['token_hash']), 64)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(session)).status_code, 200)

    def test_duplicate_and_invalid_credentials(self):
        self.register()
        self.assertEqual(self.client.post('/v1/auth/register', json={'username': 'ALICE', 'password': PASSWORD, 'device_id': 'other'}).status_code, 409)
        result = self.login(password='short')
        self.assertEqual(result.status_code, 422)
        self.assertNotIn('short', result.text)
        self.assertEqual(self.login(password='incorrect-password').status_code, 401)
        missing = self.login(username='unknownuser')
        self.assertEqual(missing.status_code, 401)
        self.assertEqual(missing.json()['detail'], 'Неверный логин или пароль.')

    def test_account_isolation_and_unauthenticated(self):
        a = self.register()
        b = self.register('bob')
        data = document([event()])
        self.assertEqual(self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': data}).status_code, 200)
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(b)).json()['document']['items'], [])
        self.assertEqual(self.client.get('/v1/sync').status_code, 401)
        self.assertEqual(self.client.put('/v1/sync', headers=self.headers(b), json={'expected_revision': 0, 'document': data, 'user_id': a['user_id']}).status_code, 422)

    def test_compare_and_swap_and_retry(self):
        a = self.register()
        body = {'expected_revision': 0, 'document': document([event()])}
        first = self.client.put('/v1/sync', headers=self.headers(a), json=body)
        self.assertEqual(first.json()['revision'], 1)
        retry = self.client.put('/v1/sync', headers=self.headers(a), json=body)
        self.assertEqual(retry.status_code, 200)
        self.assertEqual(retry.json()['revision'], 1)
        body['document']['items'][0]['Title'] = 'Changed'
        conflict = self.client.put('/v1/sync', headers=self.headers(a), json=body)
        self.assertEqual(conflict.status_code, 409)
        self.assertEqual(conflict.json()['document']['items'][0]['Title'], 'Test')

    def test_two_simultaneous_writers(self):
        a = self.register()
        def write(index):
            with TestClient(app.app) as client:
                return client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': document([event(str(index))])}).status_code
        with ThreadPoolExecutor(max_workers=2) as pool:
            result = list(pool.map(write, [1, 2]))
        self.assertEqual(sorted(result), [200, 409])
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(a)).json()['revision'], 1)

    def test_relogin_device_and_logout(self):
        a = self.register()
        second = self.login(device='pc1').json()
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(a)).status_code, 401)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(second)).status_code, 200)
        self.assertEqual(self.client.post('/v1/auth/logout', headers=self.headers(second)).status_code, 204)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(second)).status_code, 401)

    def test_expired_and_invalid_token(self):
        a = self.register()
        with app.db() as connection:
            connection.execute('UPDATE sessions SET expires_at=0')
            connection.commit()
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(a)).status_code, 401)
        self.assertEqual(self.client.get('/v1/auth/me', headers={'Authorization': 'Bearer ' + 'x' * 43}).status_code, 401)

    def test_password_change_revokes_other_sessions(self):
        a = self.register()
        b = self.login().json()
        response = self.client.post('/v1/auth/password', headers=self.headers(a), json={'old_password': PASSWORD, 'new_password': 'changed-test-password'})
        self.assertEqual(response.status_code, 200, response.text)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(a)).status_code, 401)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(b)).status_code, 401)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(response.json())).status_code, 200)
        self.assertEqual(self.login(password=PASSWORD).status_code, 401)
        self.assertEqual(self.login(password='changed-test-password').status_code, 200)

    def test_document_validation(self):
        a = self.register()
        for data in [document([event(), event()]), document([dict(event(), NextLocal='2027-02-31T10:00:00')]), document([dict(event(), Sound='C:/private.wav')]), document([dict(event(), Days=128)])]:
            response = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': data})
            self.assertEqual(response.status_code, 422, response.text)
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(a)).json()['revision'], 0)

    def test_body_limit_and_security_headers(self):
        result = self.client.post('/v1/auth/login', content=b'x' * (app.BODY_LIMIT + 1), headers={'Content-Type': 'application/json'})
        self.assertEqual(result.status_code, 413)
        self.assertEqual(self.client.get('/health').headers['cache-control'], 'no-store')
        self.assertEqual(self.client.get('/health').headers['x-content-type-options'], 'nosniff')

    def test_login_rate_limit(self):
        self.register()
        for _ in range(10):
            self.assertEqual(self.login(password='incorrect-password').status_code, 401)
        result = self.login(password=PASSWORD)
        self.assertEqual(result.status_code, 429)
        self.assertIn('retry-after', result.headers)

    def test_registration_can_be_disabled(self):
        app.REGISTRATION = False
        self.assertEqual(self.client.post('/v1/auth/register', json={'username': 'alice', 'password': PASSWORD, 'device_id': 'pc'}).status_code, 403)

    def test_delete_account_cascades(self):
        a = self.register()
        self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': document([event()])})
        result = self.client.request('DELETE', '/v1/auth/account', headers=self.headers(a), json={'password': PASSWORD})
        self.assertEqual(result.status_code, 204)
        self.assertEqual(self.client.get('/v1/auth/me', headers=self.headers(a)).status_code, 401)
        with app.db() as connection:
            for table in ['users', 'sessions', 'documents']:
                self.assertEqual(connection.execute('SELECT count(*) FROM ' + table).fetchone()[0], 0)

class NotesApiTests(unittest.TestCase):
    setUp = ApiTests.setUp
    tearDown = ApiTests.tearDown
    register = ApiTests.register
    headers = ApiTests.headers
    def note(self, id='note1', text='Первая строка\nВторая строка'):
        return {'Id': id, 'Title': 'Идеи для канала', 'Body': text, 'UpdatedUtc': '2026-11-18T09:00:00Z'}

    def test_notes_round_trip_and_isolation(self):
        a = self.register()
        b = self.register('bob')
        payload = document()
        payload['notes'] = [self.note()]
        response = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': payload})
        self.assertEqual(response.status_code, 200, response.text)
        saved = self.client.get('/v1/sync', headers=self.headers(a)).json()
        self.assertEqual(saved['document']['notes'][0], self.note())
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(b)).json()['document']['notes'], [])
        payload['notes'] = []
        result = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': saved['revision'], 'document': payload})
        self.assertEqual(result.status_code, 200)
        self.assertEqual(result.json()['document']['notes'], [])

    def test_old_client_cannot_drop_notes(self):
        a = self.register()
        payload = document()
        payload['notes'] = [self.note()]
        saved = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': payload}).json()
        old = {'schema': 1, 'items': [], 'settings': {}}
        result = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': saved['revision'], 'document': old})
        self.assertEqual(result.status_code, 422)
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(a)).json()['document']['notes'], [self.note()])

    def test_note_validation_and_quota(self):
        a = self.register()
        invalid = [[self.note(), self.note()], [dict(self.note(), Body='x' * 20001)], [dict(self.note(), UpdatedUtc='2026-11-18T09:00:00')], [dict(self.note(), Title='')], [self.note(str(i), 'x' * 20000) for i in range(11)]]
        for notes in invalid:
            payload = document()
            payload['notes'] = notes
            result = self.client.put('/v1/sync', headers=self.headers(a), json={'expected_revision': 0, 'document': payload})
            self.assertEqual(result.status_code, 422, result.text)
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(a)).json()['revision'], 0)

    def test_v1_database_migration(self):
        a = self.register()
        legacy = {'schema': 1, 'items': [event()], 'settings': {}}
        import json
        with app.db() as connection:
            connection.execute('UPDATE documents SET body=?, revision=7 WHERE user_id=?', (json.dumps(legacy), a['user_id']))
            connection.commit()
        app.initialize()
        result = self.client.get('/v1/sync', headers=self.headers(a))
        self.assertEqual(result.status_code, 200)
        self.assertEqual(result.json()['revision'], 8)
        self.assertEqual(result.json()['document']['schema'], 2)
        self.assertEqual(result.json()['document']['items'], [event()])
        self.assertEqual(result.json()['document']['notes'], [])
        app.initialize()
        self.assertEqual(self.client.get('/v1/sync', headers=self.headers(a)).json()['revision'], 8)

if __name__ == '__main__':
    unittest.main()
