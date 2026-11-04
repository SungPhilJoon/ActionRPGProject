#!/usr/bin/env bash
# Packages/com.philjoon.framework (이 프로젝트에서 고치는 원본) → 패키지 저장소(unity-framework) 로 복사 · 커밋 · (선택) 태그 · 푸시.
#
# 사용법 (ActionRPGProject 루트에서):
#   bash Tools/FrameworkPackage/sync.sh -m "커밋 메시지"                 # 복사 + 커밋
#   bash Tools/FrameworkPackage/sync.sh -m "커밋 메시지" --tag v0.2.0    # + package.json version 확인 + 태그
#   bash Tools/FrameworkPackage/sync.sh -m "커밋 메시지" --push          # + origin 으로 main · 태그 푸시
#
# 패키지 저장소 위치는 FRAMEWORK_REPO 로 바꿀 수 있다 (기본: 이 프로젝트 옆 ../unity-framework).
# 문서: Documentation/VRising/Architecture.md 2.1
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SRC="$ROOT/Packages/com.philjoon.framework"
REPO="${FRAMEWORK_REPO:-$ROOT/../unity-framework}"
MESSAGE=""
TAG=""
PUSH=0

while [ $# -gt 0 ]; do
  case "$1" in
    -m) MESSAGE="$2"; shift 2 ;;
    --tag) TAG="$2"; shift 2 ;;
    --push) PUSH=1; shift ;;
    *) echo "알 수 없는 인자: $1" >&2; exit 2 ;;
  esac
done

if [ -z "$MESSAGE" ]; then
  echo "커밋 메시지가 필요합니다: -m \"...\"" >&2
  exit 2
fi
if [ ! -f "$SRC/package.json" ]; then
  echo "원본 패키지가 없습니다: $SRC" >&2
  exit 1
fi
if [ ! -d "$REPO/.git" ]; then
  echo "패키지 저장소가 없습니다: $REPO (FRAMEWORK_REPO 로 지정 가능)" >&2
  exit 1
fi
if [ -n "$(git -C "$REPO" status --porcelain)" ]; then
  echo "패키지 저장소에 커밋하지 않은 변경이 있습니다. 먼저 정리하세요: $REPO" >&2
  exit 1
fi

if [ "$PUSH" -eq 1 ]; then
  # GitHub 에서 직접 고친 커밋(README 등)이 있으면 덮어쓰지 않도록, 커밋하기 전에 멈춘다.
  git -C "$REPO" fetch -q origin
  BEHIND="$(git -C "$REPO" rev-list --count HEAD..origin/main)"
  if [ "$BEHIND" != "0" ]; then
    echo "원격 main 에 로컬에 없는 커밋이 $BEHIND 개 있습니다:" >&2
    git -C "$REPO" log --oneline HEAD..origin/main >&2
    echo "그 변경을 원본(Packages/com.philjoon.framework)에도 반영한 뒤 'git -C $REPO merge --ff-only origin/main' 하고 다시 실행하세요." >&2
    exit 1
  fi
fi

if [ -n "$TAG" ]; then
  VERSION="$(grep -m1 '"version"' "$SRC/package.json" | sed 's/.*"version": *"\([^"]*\)".*/\1/')"
  if [ "v$VERSION" != "$TAG" ]; then
    echo "package.json version($VERSION) 과 태그($TAG) 가 다릅니다. package.json · CHANGELOG.md 를 먼저 올리세요." >&2
    exit 1
  fi
fi

# 저장소 파일을 원본과 똑같이 맞춘다 (.git, .gitignore 제외). 원본에서 지운 파일은 저장소에서도 지워진다.
git -C "$REPO" ls-files -z | grep -zv '^\.gitignore$' | (cd "$REPO" && xargs -0 -r rm -f)
cp -r "$SRC"/. "$REPO"/
git -C "$REPO" add -A

if [ -z "$(git -C "$REPO" status --porcelain)" ]; then
  echo "바뀐 파일이 없습니다."
else
  git -C "$REPO" status --short
  git -C "$REPO" commit -q -m "$MESSAGE"
  echo "커밋: $(git -C "$REPO" log --oneline -1)"
fi

if [ -n "$TAG" ]; then
  git -C "$REPO" tag -a "$TAG" -m "com.philjoon.framework ${TAG#v}"
  echo "태그: $TAG"
fi

if [ "$PUSH" -eq 1 ]; then
  git -C "$REPO" push origin main
  if [ -n "$TAG" ]; then
    git -C "$REPO" push origin "$TAG"
  fi
fi
