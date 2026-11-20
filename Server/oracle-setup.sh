#!/usr/bin/env bash
# Oracle Cloud 무료 서버(Ubuntu 24.04)에 랭킹 서버를 올린다. 서버 안에서 sudo로 실행한다.
#   sudo RANK_DOMAIN=1-2-3-4.sslip.io bash oracle-setup.sh
# 하는 일: Docker 설치 → Oracle 기본 방화벽(iptables)에 80 · 443 열기 → /opt/clockworkroll-ranking 에서 docker compose 실행
# (VCN 보안 목록의 80 · 443 열기는 Oracle 콘솔에서 따로 한다)
set -euo pipefail

: "${RANK_DOMAIN:?RANK_DOMAIN=도메인 을 넣어서 실행하세요 (예: 1-2-3-4.sslip.io)}"
APP_DIR=/opt/clockworkroll-ranking
SRC_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "== 1. Docker 설치"
if ! command -v docker >/dev/null; then
  apt-get update -y
  DEBIAN_FRONTEND=noninteractive apt-get install -y docker.io docker-compose-v2
  systemctl enable --now docker
fi

echo "== 2. 방화벽: 80 · 443 열기 (Oracle Ubuntu 이미지는 22번 말고 다 막혀 있다)"
for port in 80 443; do
  if ! iptables -C INPUT -p tcp --dport "$port" -m state --state NEW -j ACCEPT 2>/dev/null; then
    # 맨 끝의 REJECT 규칙보다 앞에 넣는다
    line=$(iptables -L INPUT --line-numbers | awk '/REJECT/ {print $1; exit}')
    iptables -I INPUT "${line:-1}" -p tcp --dport "$port" -m state --state NEW -j ACCEPT
  fi
done
if command -v netfilter-persistent >/dev/null; then netfilter-persistent save; fi

echo "== 3. 서버 파일 배치"
mkdir -p "$APP_DIR"
cp "$SRC_DIR"/ranking-server.js "$SRC_DIR"/package.json "$SRC_DIR"/Dockerfile "$SRC_DIR"/docker-compose.yml "$APP_DIR"/
echo "RANK_DOMAIN=$RANK_DOMAIN" > "$APP_DIR/.env"

echo "== 4. 실행 (Caddy가 $RANK_DOMAIN 의 HTTPS 인증서를 자동으로 받는다)"
cd "$APP_DIR"
docker compose up -d --build
sleep 3
docker compose ps
echo "확인: curl https://$RANK_DOMAIN/api/health"
