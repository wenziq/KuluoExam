#!/usr/bin/env python3
"""Package verified Unity builds, committed sources and a bounded evidence set."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def archive(folder, output):
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for path in sorted(folder.rglob('*')):
            if path.is_file():
                bundle.write(path, path.relative_to(folder))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release-copy', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    copy, evidence = args.release_copy.resolve(), args.evidence.resolve()
    for name in ('edit', 'play'):
        result = json.loads((evidence / f'{name}-summary.json').read_text())
        if result.get('failed') != '0' or result.get('skipped') != '0':
            raise SystemExit(f'{name}: require complete passing test evidence')
    if subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=no'], cwd=root):
        raise SystemExit('Commit tracked changes before packaging.')
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    delivery = root / 'Builds/Delivery'
    backups = evidence / 'previous-builds'
    backups.mkdir(exist_ok=True)
    if delivery.exists():
        shutil.move(str(delivery), str(backups / 'Delivery'))
    delivery.mkdir(parents=True)
    licenses = delivery / 'Licenses'
    licenses.mkdir()
    active = json.loads((copy / 'Packages/packages-lock.json').read_text())['dependencies']
    for package in sorted((copy / 'Library/PackageCache').iterdir()):
        name = package.name.split('@')[0]
        if name not in active:
            continue
        for path in package.rglob('*'):
            if path.is_file() and path.name.lower().startswith(('license', 'third party notices')) and path.suffix.lower() in ('.md', '.txt', '.html'):
                target = licenses / name / path.relative_to(package)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
    for source, name in [('Assets/Project/Art/Fonts/OFL.txt', 'NotoSansCJK-OFL.txt'),
                         ('Assets/Project/Art/Fonts/SOURCE.md', 'NotoSansCJK-SOURCE.md'),
                         ('Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt', 'LiberationSans-OFL.txt')]:
        shutil.copy2(root / source, licenses / name)
    for platform in ('Windows', 'Mac'):
        target = root / 'Builds' / platform
        if target.exists():
            shutil.move(str(target), str(backups / platform))
        shutil.copytree(copy / 'Builds' / platform, target, symlinks=True,
                        ignore=shutil.ignore_patterns('*BackUpThisFolder*', '.DS_Store'))
        shutil.copytree(licenses, target / 'Licenses')
        docs = target / 'Docs'
        docs.mkdir(exist_ok=True)
        for name in ('USER_GUIDE.md', 'KNOWN_ISSUES.md', 'THIRD_PARTY_NOTICES.md', 'TEST_REPORT.md', 'BUILD_AND_RUN.md', 'DELIVERY.md'):
            shutil.copy2(root / 'Docs' / name, docs / name)
        shutil.copy2(root / 'README.md', target / 'README.md')
        (target / 'MANIFEST.sha256').write_text(''.join(
            f'{digest(p)}  {p.relative_to(target)}\n' for p in sorted(target.rglob('*')) if p.is_file()))
        shutil.copytree(target, delivery / platform, symlinks=True)
    archive(delivery / 'Windows', delivery / 'Sokoban-Windows-x64.zip')
    subprocess.run(['/usr/bin/ditto', '-c', '-k', '--sequesterRsrc', '--keepParent',
                    str(delivery / 'Mac'), str(delivery / 'Sokoban-macOS.zip')], check=True)
    tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
    with zipfile.ZipFile(delivery / 'Sokoban-Source.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for name in filter(None, tracked):
            path = root / name
            if name.startswith(('Packages/', 'ProjectSettings/')):
                path = copy / name
            if path.is_file():
                bundle.write(path, name)
        for path in licenses.rglob('*'):
            if path.is_file():
                bundle.write(path, Path('Licenses') / path.relative_to(licenses))
    with zipfile.ZipFile(delivery / 'Sokoban-Verification.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for path in sorted(evidence.rglob('*')):
            if path.is_file() and 'previous-builds' not in path.relative_to(evidence).parts:
                bundle.write(path, path.relative_to(root))
    files = []
    for path in sorted(delivery.glob('*.zip')):
        with zipfile.ZipFile(path) as bundle:
            if bundle.testzip() is not None:
                raise SystemExit(f'Corrupt archive: {path}')
        files.append({'file': path.name, 'bytes': path.stat().st_size, 'sha256': digest(path)})
    manifest = {'product': '推箱子', 'unity': '6000.6.3f1', 'sourceCommit': commit,
                'sourceProfile': 'Committed project; Packages/ProjectSettings use verified release copy.',
                'verification': json.loads((evidence / 'verification.json').read_text()), 'files': files}
    (delivery / 'delivery-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    (delivery / 'SHA256SUMS.txt').write_text(''.join(f"{f['sha256']}  {f['file']}\n" for f in files))
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
