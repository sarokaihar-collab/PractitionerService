#!/usr/bin/env bash
# Smoke-тест FHIR Practitioner API.
# Запуск: BASE_URL=http://localhost:8080 ./scripts/smoke.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
CT="application/fhir+json"

echo "=== Health ==="
curl -sf "$BASE_URL/health" && echo

echo "=== POST /Practitioner ==="
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/Practitioner" \
  -H "Content-Type: $CT" \
  -H "Idempotency-Key: smoke-$(date +%s)" \
  -d @- <<'JSON'
{
  "resourceType": "Practitioner",
  "active": true,
  "name": [{"family": "Смоук", "given": ["Тест"]}],
  "telecom": [
    {"system": "phone", "value": "+7-999-999-99-99"},
    {"system": "email", "value": "smoke@example.com"}
  ],
  "qualification": [{"code": {"text": "Терапевт"}}],
  "extension": [{"url": "http://example.org/fhir/StructureDefinition/place-of-work", "valueString": "Тестовая клиника"}]
}
JSON
)
CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | head -n-1)
echo "HTTP: $CODE"
echo "$BODY"
ID=$(echo "$BODY" | python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])')
echo "ID: $ID"

echo "=== GET /Practitioner/{id} ==="
curl -s -o /dev/null -w "HTTP: %{http_code}\n" "$BASE_URL/Practitioner/$ID"

echo "=== GET /Practitioner?name=Смоук ==="
curl -s "$BASE_URL/Practitioner?name=Смоук&_count=5" | head -c 400; echo

echo "=== PUT /Practitioner/{id} (If-Match: \"1\") ==="
curl -s -o /dev/null -w "HTTP: %{http_code}\n" -X PUT "$BASE_URL/Practitioner/$ID" \
  -H "Content-Type: $CT" \
  -H "If-Match: \"1\"" \
  -d @- <<'JSON'
{
  "resourceType": "Practitioner",
  "active": false,
  "name": [{"family": "Смоук-Обновлённый", "given": ["Тест"]}],
  "telecom": [
    {"system": "phone", "value": "+7-999-999-99-98"},
    {"system": "email", "value": "smoke2@example.com"}
  ],
  "qualification": [{"code": {"text": "Хирург"}}],
  "extension": [{"url": "http://example.org/fhir/StructureDefinition/place-of-work", "valueString": "Обновлённая клиника"}]
}
JSON

echo "=== DELETE /Practitioner/{id} (ожидаем 204) ==="
curl -s -o /dev/null -w "HTTP: %{http_code}\n" -X DELETE "$BASE_URL/Practitioner/$ID"

echo "=== GET удалённого (ожидаем 410) ==="
curl -s -o /dev/null -w "HTTP: %{http_code}\n" "$BASE_URL/Practitioner/$ID"

echo "=== DELETE повторно (ожидаем 410) ==="
curl -s -o /dev/null -w "HTTP: %{http_code}\n" -X DELETE "$BASE_URL/Practitioner/$ID"

echo
echo "Smoke-тест завершён."