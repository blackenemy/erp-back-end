#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
# Seed Rules into RuleService
# ═══════════════════════════════════════════════════════════════════
# Usage: ./scripts/seed-rules.sh [RULE_SERVICE_URL]
# Example: ./scripts/seed-rules.sh http://localhost:5002

set -e

RULE_SERVICE_URL="${1:-http://localhost:5002}"
RULES_FILE="data/rules.json"

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║          Seeding Rules into RuleService                      ║"
echo "║          URL: $RULE_SERVICE_URL"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Check if RuleService is accessible
echo "🔍 Checking RuleService connectivity..."
if ! curl -s "$RULE_SERVICE_URL/health" > /dev/null; then
    echo "❌ Error: Cannot connect to RuleService at $RULE_SERVICE_URL"
    echo "   Make sure RuleService is running on port 5002"
    exit 1
fi
echo "✅ RuleService is healthy"
echo ""

# Delete existing rules
echo "🗑️  Deleting existing rules..."
RULES=$(curl -s "$RULE_SERVICE_URL/rules")
RULE_COUNT=$(echo "$RULES" | jq 'length')

if [ "$RULE_COUNT" -gt 0 ]; then
    echo "$RULES" | jq -r '.[] | .id' | while read -r RULE_ID; do
        curl -s -X DELETE "$RULE_SERVICE_URL/rules/$RULE_ID" > /dev/null
        echo "   ✓ Deleted rule: $RULE_ID"
    done
else
    echo "   (No existing rules to delete)"
fi
echo ""

# Create new rules from JSON file
echo "📝 Creating new rules from $RULES_FILE..."
if [ ! -f "$RULES_FILE" ]; then
    echo "❌ Error: $RULES_FILE not found"
    exit 1
fi

TOTAL_RULES=$(jq 'length' "$RULES_FILE")
CREATED=0

jq -c '.[]' "$RULES_FILE" | while read -r RULE; do
    RULE_NAME=$(echo "$RULE" | jq -r '.name')
    
    RESPONSE=$(curl -s -X POST "$RULE_SERVICE_URL/rules" \
        -H "Content-Type: application/json" \
        -d "$RULE")
    
    RULE_ID=$(echo "$RESPONSE" | jq -r '.id // empty')
    
    if [ -n "$RULE_ID" ]; then
        echo "   ✅ Created: $RULE_NAME (ID: $RULE_ID)"
        ((CREATED++))
    else
        echo "   ❌ Failed to create: $RULE_NAME"
        echo "      Response: $RESPONSE"
    fi
done

echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                    Seeding Complete                           ║"
echo "║                    Total: $TOTAL_RULES rules                       ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "✨ Sample rules loaded successfully!"
echo ""
echo "📍 Next steps:"
echo "   1. Test single quote: curl -X POST http://localhost:5001/quotes/price \\"
echo "      -H 'Content-Type: application/json' \\"
echo "      -d '{\"weightKg\": 12, \"originZip\": \"10100\", \"destinationZip\": \"95120\"}'"
echo ""
echo "   2. View API docs: http://localhost:5001/scalar/v1"
echo ""
