# GitBatchPush — 대용량 저장소를 나눠서 커밋/푸시

GitHub 는 **푸시 1회 2GB**, **파일 1개 100MB** 제한이 있습니다. 에셋이 수 GB 인 Unity 프로젝트를 한 번에 올리면 푸시가 실패하므로,
커밋되지 않은 파일을 용량 기준으로 나눠 **커밋 → 푸시** 를 배치마다 반복합니다.

## 사용법 (저장소 루트에서, Git Bash)

```bash
# 0) 100MB 넘는 파일은 먼저 LFS 로 (make_batches 가 검사해서 알려줌)
git lfs track "Assets/…/큰파일.exr"
git add .gitattributes "Assets/…/큰파일.exr" "Assets/…/큰파일.exr.meta"
git commit -m "LFS: 큰 파일" && git push

# 1) 배치 계획 (기본 450MB, 결과는 Tools/GitBatchPush/.work/)
python Tools/GitBatchPush/make_batches.py --limit-mb 450

# 2) 미리보기
bash Tools/GitBatchPush/push_batches.sh -m "프로젝트 업로드" --dry-run

# 3) 실행 (끊기면 같은 명령을 다시 실행하면 이어서 진행)
bash Tools/GitBatchPush/push_batches.sh -m "프로젝트 업로드"
```

커밋 메시지에 공동 작성자를 넣으려면 `CO_AUTHOR="Name <mail>" bash …` 처럼 환경 변수로 줍니다.

## 옵션

| 스크립트 | 옵션 | 기본값 | 설명 |
|---|---|---|---|
| make_batches.py | `--limit-mb` | 450 | 배치당 최대 용량 |
| | `--include-modified` | 끔 | 수정된 추적 파일도 포함 |
| | `--out` | `Tools/GitBatchPush/.work` | 배치 목록 폴더 |
| push_batches.sh | `-r/--remote` | origin | |
| | `-b/--branch` | 현재 브랜치 | |
| | `-m/--message` | `Batch upload` | 커밋 제목 앞부분 → `<메시지> (n/N): <대표 폴더>` |
| | `--retry` | 2 | 배치당 푸시 시도 횟수 |
| | `--dry-run` | | 커밋/푸시 없이 계획만 출력 |

## 동작 방식

**make_batches.py**
1. `git ls-files --others --exclude-standard` 로 미추적 파일 수집 (.gitignore 제외)
2. 프로젝트 파일 먼저, `Assets/ExternalAssets` 는 뒤에 경로 순 → 같은 폴더와 `.meta` 가 같은 배치에 모임
3. 100MB 초과 파일이 LFS(`filter=lfs`)가 아니면 목록을 출력하고 중단
4. 누적 용량이 한도를 넘기 직전에 배치를 끊어 `batch_NNN.txt`(경로 목록)와 `index.tsv`(이름·파일 수·MB·대표 폴더) 작성

**push_batches.sh** — `index.tsv` 순서대로
1. `.done` 이 있으면 건너뜀
2. 마지막 커밋 제목이 이 배치와 같으면(커밋 후 푸시가 끊긴 경우) 푸시만
3. `git add --pathspec-from-file` → `git commit` → `git push` (실패 시 재시도, 그래도 실패면 중단)
4. `git ls-remote` 로 리모트 브랜치가 HEAD 와 같은지 확인 후 `.done` 표시

## 주의

- **한 번에 하나만 실행.** 잠금 파일(`.work/.lock`)로 막지만, 다른 터미널에서 수동으로 `git add/commit` 하지 마세요.
- 배치 도중 프로젝트 파일이 바뀌면 그 변경도 해당 배치에 그대로 들어갑니다. 실행 중에는 Unity 에서 저장/임포트를 피하세요.
- 저장소가 수 GB 면 GitHub 권장 크기(5GB 이하)를 넘습니다. 푸시는 되지만 클론이 느립니다.
- `.work/` 는 `.gitignore` 에 등록되어 있습니다.
