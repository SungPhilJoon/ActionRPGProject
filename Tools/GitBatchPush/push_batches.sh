#!/usr/bin/env bash
# make_batches.py 가 만든 배치를 하나씩 커밋하고 바로 푸시한다.
#
# 사용 예 (저장소 루트에서):
#   bash Tools/GitBatchPush/push_batches.sh -m "프로젝트 업로드"
#   bash Tools/GitBatchPush/push_batches.sh -r origin -b master -m "에셋 추가" --dry-run
#
# - 끝난 배치는 <work>/<batch>.done 으로 표시 → 중간에 끊겨도 같은 명령으로 이어서 진행
# - 커밋은 됐는데 푸시가 끊긴 배치는 다시 커밋하지 않고 푸시만 한다
# - 푸시 실패 시 RETRY 번 재시도 후 중단 (실패한 상태로 다음 배치를 쌓지 않음)
# - 같은 작업 폴더로 두 번 실행되지 않도록 잠금 파일을 쓴다
# - 푸시 후 리모트 브랜치가 HEAD 와 같은지 확인한다

set -u

REMOTE="origin"
BRANCH="$(git rev-parse --abbrev-ref HEAD)"
PREFIX="Batch upload"
WORK="Tools/GitBatchPush/.work"
RETRY=2
DRY_RUN=0

while [ $# -gt 0 ]; do
  case "$1" in
    -r|--remote)  REMOTE="$2"; shift 2 ;;
    -b|--branch)  BRANCH="$2"; shift 2 ;;
    -m|--message) PREFIX="$2"; shift 2 ;;
    -w|--work)    WORK="$2"; shift 2 ;;
    --retry)      RETRY="$2"; shift 2 ;;
    --dry-run)    DRY_RUN=1; shift ;;
    -h|--help)    sed -n '2,13p' "$0"; exit 0 ;;
    *) echo "unknown option: $1"; exit 1 ;;
  esac
done

if [ ! -f "$WORK/index.tsv" ]; then
  echo "배치 목록이 없습니다. 먼저 python Tools/GitBatchPush/make_batches.py 를 실행하세요."
  exit 1
fi

LOCK="$WORK/.lock"
if [ -f "$LOCK" ] && kill -0 "$(cat "$LOCK")" 2>/dev/null; then
  echo "이미 실행 중입니다 (PID $(cat "$LOCK")). 중복 실행하면 커밋이 꼬입니다."
  exit 1
fi
echo $$ > "$LOCK"
trap 'rm -f "$LOCK"' EXIT

total=$(wc -l < "$WORK/index.tsv")
co_author_line=""
if [ -n "${CO_AUTHOR:-}" ]; then
  co_author_line=$'\n\n'"Co-Authored-By: $CO_AUTHOR"
fi

echo "remote=$REMOTE branch=$BRANCH batches=$total dry-run=$DRY_RUN"

while IFS=$'\t' read -r name count mb label; do
  n=$((10#${name//[!0-9]/}))
  subject="$PREFIX ($n/$total): $label"

  if [ -f "$WORK/$name.done" ]; then
    echo "[$n/$total] skip (already pushed)"
    continue
  fi

  if [ $DRY_RUN -eq 1 ]; then
    echo "[$n/$total] would commit+push: $count files, $mb MB — $subject"
    continue
  fi

  if [ "$(git log -1 --format=%s 2>/dev/null)" = "$subject" ]; then
    echo "[$n/$total] already committed, pushing only"
  else
    if ! git -c core.quotepath=off add --pathspec-from-file="$WORK/$name" 2> "$WORK/$name.add.log"; then
      echo "[$n/$total] FAILED git add: $(tail -2 "$WORK/$name.add.log" | tr '\n' ' ')"
      exit 1
    fi

    if ! git commit -q -F - <<EOF
$subject

${count} files, ${mb} MB${co_author_line}
EOF
    then
      echo "[$n/$total] FAILED git commit"
      exit 1
    fi
  fi

  ok=0
  for attempt in $(seq 1 "$RETRY"); do
    if git push "$REMOTE" "$BRANCH" > "$WORK/$name.push.log" 2>&1; then
      ok=1
      break
    fi
    echo "[$n/$total] push attempt $attempt failed: $(grep -iE 'error|fatal|reject|remote:' "$WORK/$name.push.log" | head -3 | tr '\n' ' ')"
  done

  head_sha=$(git rev-parse HEAD)
  remote_sha=$(git ls-remote "$REMOTE" "refs/heads/$BRANCH" | cut -f1)
  if [ $ok -ne 1 ] || [ "$head_sha" != "$remote_sha" ]; then
    echo "[$n/$total] FAILED push - stopping (local HEAD ${head_sha:0:7}, remote ${remote_sha:0:7})"
    exit 1
  fi

  touch "$WORK/$name.done"
  echo "[$n/$total] pushed ${head_sha:0:7} - ${count} files, ${mb} MB ($label)"
done < "$WORK/index.tsv"

echo "ALL DONE"
