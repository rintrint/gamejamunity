"""Publish a verified Windows ZIP with the existing Git credential manager account.

No credentials are written to disk or printed. --check is read-only.
"""
import argparse
import json
import os
import subprocess
import urllib.error
import urllib.parse
import urllib.request

p = argparse.ArgumentParser()
p.add_argument('--repo', required=True)
p.add_argument('--check', action='store_true')
p.add_argument('--tag')
p.add_argument('--target')
p.add_argument('--zip')
p.add_argument('--body-file')
args = p.parse_args()
credential = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n',
                            text=True, capture_output=True, check=True)
fields = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
token = fields.get('password')
if not token:
    raise SystemExit('The existing Git credential manager did not return a GitHub credential.')

def request(url, method='GET', payload=None, binary=False):
    headers = {'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
               'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'SealGugu-Release-Tool'}
    body = None
    if payload is not None:
        body = payload if binary else json.dumps(payload).encode('utf-8')
        headers['Content-Type'] = 'application/zip' if binary else 'application/json'
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=body, headers=headers, method=method), timeout=240) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        # Report status and API message, never request headers or the credential.
        message = json.loads(error.read()).get('message', 'GitHub request failed')
        raise RuntimeError(f'GitHub HTTP {error.code}: {message}') from None

base = 'https://api.github.com/repos/' + args.repo
repo = request(base)
if args.check:
    print(json.dumps({'repository': repo['full_name'], 'default_branch': repo['default_branch'],
                      'can_push': repo.get('permissions', {}).get('push', False)}, ensure_ascii=False))
else:
    if not all((args.tag, args.target, args.zip, args.body_file)):
        raise SystemExit('--tag, --target, --zip and --body-file are required for publication.')
    with open(args.body_file, encoding='utf-8-sig') as f:
        description = f.read()
    # A draft is only made public after the build upload completes.
    release = request(base + '/releases', 'POST', {'tag_name': args.tag, 'target_commitish': args.target,
        'name': '海豹咕咕 ' + args.tag, 'body': description, 'draft': True, 'prerelease': False})
    upload = release['upload_url'].split('{')[0] + '?name=' + urllib.parse.quote(os.path.basename(args.zip))
    with open(args.zip, 'rb') as f:
        asset = request(upload, 'POST', f.read(), True)
    if asset.get('state') != 'uploaded' or asset['size'] != os.path.getsize(args.zip):
        raise RuntimeError('The uploaded release asset could not be verified; release remains draft.')
    final = request(base + '/releases/' + str(release['id']), 'PATCH', {'draft': False})
    # GitHub replaces the temporary draft URL when the version tag is published.
    final = request(base + '/releases/' + str(release['id']))
    asset = next(item for item in final['assets'] if item['id'] == asset['id'])
    print(json.dumps({'release': final['html_url'], 'asset': asset['browser_download_url'],
                      'size': asset['size'], 'tag': final['tag_name']}, ensure_ascii=False))
