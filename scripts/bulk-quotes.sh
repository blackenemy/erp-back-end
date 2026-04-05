#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
# Submit Bulk Quotes from CSV
# ═══════════════════════════════════════════════════════════════════
# Usage: ./scripts/bulk-quotes.sh [CSV_FILE] [PRICING_SERVICE_URL]
# Example: ./scripts/bulk-quotes.sh data/bulk_quotes.csv http://localhost:5001

set -e

CSV_FILE="${1:-data/bulk_quotes.csv}"
PRICING_SERVICE_URL="${2:-http://localhost:5001}"

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║          Submitting Bulk Quotes                              ║"
echo "║          CSV: $CSV_FILE"
echo "║          URL: $PRICING_SERVICE_URL"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Check if file exists
if [ ! -f "$CSV_FILE" ]; then
    echo "❌ Error: CSV file not found: $CSV_FILE"
    exit 1
fi

# Check if PricingService is accessible
echo "🔍 Checking PricingService connectivity..."
if ! curl -s "$PRICING_SERVICE_URL/health" > /dev/null; then
    echo "❌ Error: Cannot connect to PricingService at $PRICING_SERVICE_URL"
    echo "   Make sure PricingService is running on port 5001"
    exit 1
fi
echo "✅ PricingService is healthy"
echo ""

# Parse CSV and create JSON payload
echo "📋 Preparing bulk quote request..."
ITEMS="["
FIRST=true
LINE_NO=0

while IFS=',' read -r weightKg originZip destinationZip description; do
    # Skip header
    ((LINE_NO++))
    if [ $LINE_NO -eq 1 ]; then
        continue
    fi
    
    # Add comma separator
    if [ "$FIRST" = false ]; then
        ITEMS="$ITEMS,"
    fi
    FIRST=false
    
    # Add JSON object
    ITEMS="$ITEMS{\"weightKg\":$weightKg,\"originZip\":\"$originZip\",\"destinationZip\":\"$destinationZip\"}"
    
    echo "   ✓ Added: ${description// /_} (${weightKg}kg)"
done < "$CSV_FILE"

ITEMS="$ITEMS]"
PAYLOAD="{\"items\":$ITEMS}"

echo ""
echo "📤 Submitting $((LINE_NO - 1)) quotes..."
echo ""

# Submit bulk quote
RESPONSE=$(curl -s -X POST "$PRICING_SERVICE_URL/quotes/bulk" \
    -H "Content-Type: application/json" \
    -d "$PAYLOAD")

JOB_ID=$(echo "$RESPONSE" | jq -r '.jobId // empty')
STATUS=$(echo "$RESPONSE" | jq -r '.status // empty')

if [ -z "$JOB_ID" ]; then
    echo "❌ Error: Failed to submit bulk quote"
    echo "   Response: $RESPONSE"
    exit 1
fi

echo "✅ Bulk quote submitted successfully"
echo ""
echo "🆔 Job ID: $JOB_ID"
echo "📊 Status: $STATUS"
echo ""

# Poll for completion
echo "⏳ Waiting for processing to complete..."
echo ""

POLL_COUNT=0
MAX_POLLS=60
POLL_INTERVAL=1

while [ $POLL_COUNT -lt $MAX_POLLS ]; do
    sleep $POLL_INTERVAL
    
    JOB=$(curl -s "$PRICING_SERVICE_URL/jobs/$JOB_ID")
    CURRENT_STATUS=$(echo "$JOB" | jq -r '.status // empty')
    RESULT_COUNT=$(echo "$JOB" | jq '.results | length')
    
    printf "\r⏳ Status: %-12s | Results: %d/%d" "$CURRENT_STATUS" "$RESULT_COUNT" "$((LINE_NO - 1))"
    
    if [ "$CURRENT_STATUS" = "completed" ] || [ "$CURRENT_STATUS" = "failed" ]; then
        echo ""
        break
    fi
    
    ((POLL_COUNT++))
done

echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                Processing Complete                           ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Display results
if [ "$CURRENT_STATUS" = "completed" ]; then
    echo "✅ All quotes processed successfully!"
    echo ""
    echo "📊 Results Summary:"
    echo "$JOB" | jq '.results[] | {
        basePrice,
        discount,
        surcharge,
        finalPrice,
        appliedRules
    }' | head -50
else
    echo "❌ Processing failed or timed out"
    echo "   Status: $CURRENT_STATUS"
fi

echo ""
echo "💡 To view full results:"
echo "   curl $PRICING_SERVICE_URL/jobs/$JOB_ID | jq ."
echo ""
