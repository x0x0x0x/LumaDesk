"""Local administrator operations. Never exposes passwords in command arguments."""
import argparse
import getpass
import sqlite3
from pathlib import Path
import app

parser = argparse.ArgumentParser()
sub = parser.add_subparsers(dest='command', required=True)
backup = sub.add_parser('backup')
backup.add_argument('destination')
reset = sub.add_parser('reset-password')
reset.add_argument('username')
args = parser.parse_args()
if args.command == 'backup':
    target = Path(args.destination)
    target.parent.mkdir(parents=True, exist_ok=True)
    with app.db() as source, sqlite3.connect(target) as destination:
        source.backup(destination)
    print('Backup saved:', target)
else:
    password = getpass.getpass('New password (12–128 characters): ')
    if not 12 <= len(password) <= 128 or password != getpass.getpass('Repeat password: '):
        raise SystemExit('Passwords must match and contain 12–128 characters.')
    encoded = app.encode_password(password)
    with app.db() as connection:
        connection.execute('BEGIN IMMEDIATE')
        user = connection.execute('SELECT id FROM users WHERE username=?', (args.username.lower(),)).fetchone()
        if user is None:
            raise SystemExit('Account not found.')
        connection.execute('UPDATE users SET password_hash=? WHERE id=?', (encoded, user['id']))
        connection.execute('DELETE FROM sessions WHERE user_id=?', (user['id'],))
        connection.commit()
    print('Password changed; existing sessions revoked.')
