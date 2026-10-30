"""커밋되지 않은 파일을 용량 기준 배치로 나눈다 (push_batches.sh 가 이 결과를 순서대로 커밋/푸시).

사용 예:
    python Tools/GitBatchPush/make_batches.py --limit-mb 450

- 대상: `git ls-files --others --exclude-standard` (미추적, .gitignore 제외) + 수정된 추적 파일(--include-modified)
- 순서: 프로젝트 파일 먼저, Assets/ExternalAssets 는 뒤에 경로 순 (같은 폴더/.meta 가 같은 배치에 모이도록)
- GitHub 단일 파일 제한(100MB)을 넘는데 LFS 로 추적되지 않은 파일이 있으면 배치를 만들지 않고 중단한다.
"""
import argparse
import os
import subprocess
import sys

GITHUB_FILE_LIMIT = 100 * 1024 * 1024
EXTERNAL_PREFIX = "Assets/ExternalAssets"


def git(InRepo, *InArgs):
    return subprocess.run(["git", "-c", "core.quotepath=off", *InArgs], cwd=InRepo, capture_output=True, check=True).stdout


def listFiles(InRepo, InIncludeModified):
    paths = [p.decode("utf-8") for p in git(InRepo, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0") if p]
    if InIncludeModified == True:
        modified = [p.decode("utf-8") for p in git(InRepo, "ls-files", "--modified", "-z").split(b"\0") if p]
        paths.extend(p for p in modified if os.path.exists(os.path.join(InRepo, p)))
    return sorted(set(paths), key=lambda p: (1 if p.startswith(EXTERNAL_PREFIX) else 0, p))


def findUntrackedLargeFiles(InRepo, InPaths):
    large = [p for p in InPaths if os.path.getsize(os.path.join(InRepo, p)) > GITHUB_FILE_LIMIT]
    if len(large) == 0:
        return []

    # check-attr 로 filter=lfs 가 걸려 있는지 확인
    out = subprocess.run(["git", "-c", "core.quotepath=off", "check-attr", "filter", "--stdin", "-z"], cwd=InRepo,
                         input="\0".join(large).encode("utf-8") + b"\0", capture_output=True, check=True).stdout
    fields = out.split(b"\0")
    notLfs = []
    for i in range(0, len(fields) - 2, 3):
        if fields[i + 2] != b"lfs":
            notLfs.append(fields[i].decode("utf-8"))
    return notLfs


def label(InFiles):
    tops = []
    for f in InFiles:
        parts = f.split("/")
        if parts[0] == "Assets" and len(parts) > 2 and parts[1] == "ExternalAssets":
            top = parts[2]
        elif parts[0] == "Assets" and len(parts) > 1:
            top = parts[1]
        else:
            top = parts[0]
        top = top.replace(".meta", "")
        if top not in tops:
            tops.append(top)
    return ", ".join(tops[:4]) + (" 외" if len(tops) > 4 else "")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--repo", default=".", help="저장소 루트 (기본: 현재 폴더)")
    parser.add_argument("--out", default="Tools/GitBatchPush/.work", help="배치 목록을 쓸 폴더 (저장소 기준, .gitignore 됨)")
    parser.add_argument("--limit-mb", type=int, default=450, help="배치당 최대 용량 MB (GitHub 푸시 1회 2GB 제한보다 충분히 작게)")
    parser.add_argument("--include-modified", action="store_true", help="수정된 추적 파일도 포함")
    args = parser.parse_args()

    repo = os.path.abspath(args.repo)
    outDir = os.path.join(repo, args.out)
    limit = args.limit_mb * 1024 * 1024

    paths = listFiles(repo, args.include_modified)
    if len(paths) == 0:
        print("올릴 파일이 없습니다.")
        return 0

    notLfs = findUntrackedLargeFiles(repo, paths)
    if len(notLfs) > 0:
        print("100MB 를 넘는데 LFS 로 추적되지 않은 파일이 있습니다. 먼저 `git lfs track \"<경로>\"` 후 다시 실행하세요:")
        for p in notLfs:
            print(f"  {os.path.getsize(os.path.join(repo, p)) / 1048576:7.1f} MB  {p}")
        return 2

    batches = []
    current, currentSize = [], 0
    for p in paths:
        size = os.path.getsize(os.path.join(repo, p))
        if len(current) > 0 and currentSize + size > limit:
            batches.append((current, currentSize))
            current, currentSize = [], 0
        current.append(p)
        currentSize += size
    if len(current) > 0:
        batches.append((current, currentSize))

    os.makedirs(outDir, exist_ok=True)
    for name in os.listdir(outDir):
        os.remove(os.path.join(outDir, name))

    with open(os.path.join(outDir, "index.tsv"), "w", encoding="utf-8", newline="\n") as index:
        for i, (files, size) in enumerate(batches, 1):
            name = f"batch_{i:03d}.txt"
            with open(os.path.join(outDir, name), "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(files) + "\n")
            index.write(f"{name}\t{len(files)}\t{size / 1048576:.1f}\t{label(files)}\n")
            print(f"{i:3d}  {len(files):6d} files  {size / 1048576:8.1f} MB  {label(files)}")

    total = sum(size for _, size in batches)
    print(f"\n{len(batches)} batches, {len(paths)} files, {total / 1048576:.1f} MB → {outDir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
