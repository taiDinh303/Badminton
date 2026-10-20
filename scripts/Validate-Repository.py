"""Offline documentation/project-reference validation. Run from any directory."""
from pathlib import Path
from urllib.parse import unquote, urlparse
import re
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
errors = []
links = 0
tables = 0
files = sorted((root / 'docs').glob('*.md')) + [
    root / 'README.md', root / 'AGENTS.md', root / 'Backend/ARCHITECTURE.md', root / 'Frontend/README.md'
]


def anchors(text):
    result, used = set(), {}
    for heading in re.findall(r'^#{1,6}\s+(.+)$', re.sub(r'```.*?```', '', text, flags=re.S), re.M):
        value = ''.join(char for char in heading.lower().replace('`', '') if char.isalnum() or char in ' _-').replace(' ', '-')
        count = used.get(value, 0)
        used[value] = count + 1
        result.add(value if count == 0 else f'{value}-{count}')
    return result


for file in files:
    text = file.read_text(encoding='utf-8-sig')
    label = file.relative_to(root)
    if len(re.findall(r'^# ', re.sub(r'```.*?```', '', text, flags=re.S), re.M)) != 1:
        errors.append(f'{label}: expected one H1')
    if len(re.findall(r'^```', text, re.M)) % 2:
        errors.append(f'{label}: unbalanced code fences')
    if '\ufffd' in text or any(ord(char) < 32 and char not in '\n\r\t' for char in text):
        errors.append(f'{label}: invalid/control characters')
    expected = None
    for number, line in enumerate(text.splitlines(), 1):
        if line.startswith('|'):
            columns = len(re.findall(r'(?<!\\)\|', line)) - 1
            if expected is None:
                expected = columns
                tables += 1
            elif expected != columns:
                errors.append(f'{label}:{number}: inconsistent table columns')
        else:
            expected = None
    for target in re.findall(r'\[[^\]]*\]\(([^)]+)\)', text):
        if urlparse(target).scheme:
            continue
        path, _, fragment = unquote(target).partition('#')
        destination = file.parent / path if path else file
        links += 1
        if not destination.exists():
            errors.append(f'{label}: missing link {target}')
        elif fragment and destination.suffix == '.md' and fragment not in anchors(destination.read_text(encoding='utf-8-sig')):
            errors.append(f'{label}: missing anchor {target}')

for project in (root / 'Backend').rglob('*.csproj'):
    if any(part in ('bin', 'obj') for part in project.parts):
        continue
    for reference in ET.parse(project).findall('.//ProjectReference'):
        if not (project.parent / reference.attrib['Include'].replace('\\', '/')).exists():
            errors.append(f'{project.relative_to(root)}: missing project reference')
for project in ET.parse(root / 'Backend/Badminton.slnx').findall('.//Project'):
    if not (root / 'Backend' / project.attrib['Path']).exists():
        errors.append(f'missing solution reference: {project.attrib["Path"]}')

print(f'Checked {len(files)} Markdown files, {links} links, {tables} tables and backend project references; {len(errors)} errors.')
for error in errors:
    print(error.encode('ascii', errors='backslashreplace').decode('ascii'))
sys.exit(1 if errors else 0)
