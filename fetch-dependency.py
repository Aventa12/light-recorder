"""Download the pinned recording dependency; no third-party Python packages required."""
import hashlib
import io
from pathlib import Path
import urllib.request
import zipfile

root = Path(__file__).resolve().parent
url = 'https://api.nuget.org/v3-flatcontainer/screenrecorderlib/7.0.1/screenrecorderlib.7.0.1.nupkg'
with urllib.request.urlopen(url, timeout=60) as response:
    package = response.read()
expected_hash = '5032db2821316391fe0322739d658bd8ce4ef059b02542e97b09fa1e47c04e84'
if hashlib.sha256(package).hexdigest() != expected_hash:
    raise RuntimeError('Downloaded package checksum does not match ScreenRecorderLib 7.0.1.')
destination = root / 'vendor' / 'screenrecorderlib'
with zipfile.ZipFile(io.BytesIO(package)) as archive:
    for name in ('build/x64/ScreenRecorderLib.dll', 'LICENSE'):
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(archive.read(name))
print('ScreenRecorderLib 7.0.1 downloaded; SHA-256:', hashlib.sha256(package).hexdigest())
