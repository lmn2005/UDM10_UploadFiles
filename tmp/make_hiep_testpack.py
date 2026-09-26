from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
import hashlib
import json

ROOT = Path("/Users/tanhiep/Desktop/UDM10_UploadFiles")
OUT = ROOT / "outputs/01a0d91e-3830-73e1-a279-82a9abbbc916/Test_Files_LTM_Hiep.zip"
STAGE = ROOT / "tmp/hiep_test_data"
STAGE.mkdir(parents=True, exist_ok=True)

def write_repeated(path: Path, size: int, seed: int) -> None:
    block = (f"UDM10-TEST-DATA-{seed:02d}|chunk-check|0123456789abcdef|\n".encode("ascii") * 128)
    with path.open("wb") as stream:
        left = size
        while left:
            part = block[: min(left, len(block))]
            stream.write(part)
            left -= len(part)

def write_text_exact(path: Path, size: int) -> None:
    phrase = "Dữ liệu kiểm thử upload UDM10. Tên tiếng Việt, dấu cách và ký tự Unicode.\r\n".encode("utf-8")
    data = phrase * (size // len(phrase))
    data += b"A" * (size - len(data))
    path.write_bytes(data)

files = []
def add_file(rel: str, size: int, make, seed: int = 0):
    path = STAGE / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    if make == "empty":
        path.write_bytes(b"")
    elif make == "text":
        write_text_exact(path, size)
    else:
        write_repeated(path, size, seed)
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    files.append({"path": rel, "size_bytes": size, "sha256": digest})

add_file("00_file_rong.txt", 0, "empty")
add_file("01_file_nho_tieng_Viet_10KB.txt", 10 * 1024, "text")
add_file("02_dung_mot_chunk_64KiB.bin", 64 * 1024, "pattern", 2)
add_file("03_chunk_cong_1_byte_64KiB.bin", 64 * 1024 + 1, "pattern", 3)
add_file("04_file_1MiB.bin", 1 * 1024 * 1024, "pattern", 4)
add_file("05_file_10MiB.bin", 10 * 1024 * 1024, "pattern", 5)
add_file("06_file_20MiB.bin", 20 * 1024 * 1024, "pattern", 6)
add_file("07_file_cancel_100MiB.bin", 100 * 1024 * 1024, "pattern", 7)
add_file("08_trung_ten_A/bao_cao.txt", 32 * 1024, "text")
add_file("08_trung_ten_B/bao_cao.txt", 48 * 1024, "pattern", 8)

readme = """UDM10 - Bộ file mẫu cho test case của Tấn Hiệp

Giải nén ZIP trước khi chọn file trong Client. Các file .bin có kích thước đúng như tên; nội dung có mẫu lặp để ZIP gọn, phù hợp kiểm thử kích thước, chunk, queue, cancel và cleanup. File mẫu không phải dữ liệu ngẫu nhiên để benchmark hiệu năng chính thức.

00_file_rong.txt: kiểm tra file 0 byte.
01_file_nho_tieng_Viet_10KB.txt: kiểm tra file nhỏ, Unicode và tên file có tiếng Việt.
02_dung_mot_chunk_64KiB.bin: đúng một chunk cấu hình mặc định 65536 byte.
03_chunk_cong_1_byte_64KiB.bin: lớn hơn một chunk 1 byte.
04_file_1MiB.bin: upload nhỏ.
05_file_10MiB.bin: dùng để tạo bộ 10 file cho stress mức 1.
06_file_20MiB.bin: dùng để tạo bộ 30 file cho stress mức 2.
07_file_cancel_100MiB.bin: dùng cho cancel khi upload đang chạy.
08_trung_ten_A/bao_cao.txt và 08_trung_ten_B/bao_cao.txt: cùng tên, khác nội dung, dùng thử xử lý trùng tên.

Tạo bộ stress trên Windows PowerShell sau khi giải nén:

$src = Join-Path $PSScriptRoot '05_file_10MiB.bin'
$dst = Join-Path $PSScriptRoot 'stress_10x10MiB'
New-Item -ItemType Directory -Force $dst | Out-Null
1..10 | ForEach-Object { Copy-Item $src (Join-Path $dst ('file_{0:D2}_10MiB.bin' -f $_)) -Force }

$src = Join-Path $PSScriptRoot '06_file_20MiB.bin'
$dst = Join-Path $PSScriptRoot 'stress_30x20MiB'
New-Item -ItemType Directory -Force $dst | Out-Null
1..30 | ForEach-Object { Copy-Item $src (Join-Path $dst ('file_{0:D2}_20MiB.bin' -f $_)) -Force }

SHA-256 từng file được ghi trong manifest.json. File 10 MiB/20 MiB được sao chép với tên khác để tạo hàng đợi; nội dung trùng nhau là phù hợp với kiểm thử truyền và tải, nhưng không phù hợp để đo hiệu năng chính thức.
"""
(STAGE / "README.txt").write_text(readme, encoding="utf-8")
(STAGE / "manifest.json").write_text(json.dumps({"files": files}, ensure_ascii=False, indent=2), encoding="utf-8")

with ZipFile(OUT, "w", compression=ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(STAGE.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(STAGE).as_posix())

print(json.dumps({"output": str(OUT), "file_count": len(files), "expanded_bytes": sum(f["size_bytes"] for f in files), "zip_bytes": OUT.stat().st_size}, ensure_ascii=False))
