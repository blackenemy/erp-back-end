# Scripts Guide

ตัวช่วยสำหรับ seeding data และทดสอบ API

## Available Scripts

### 1. `seed-rules.sh` — Seed Sample Rules

ลบกฎเดิมทั้งหมด และสร้างกฎตัวอย่างจาก `data/rules.json`

**Usage:**

```bash
# Basic (uses default URL: http://localhost:5002)
./scripts/seed-rules.sh

# With custom URL
./scripts/seed-rules.sh http://ruleservice:5002
```

**What it does:**

1. ✅ ตรวจสอบ RuleService connectivity
2. 🗑️ ลบกฎเดิมทั้งหมด
3. 📝 สร้างกฎใหม่จาก `data/rules.json`
4. 📊 แสดง summary

**Example Output:**

```
╔═══════════════════════════════════════════════════════════════╗
║          Seeding Rules into RuleService                      ║
║          URL: http://localhost:5002
╚═══════════════════════════════════════════════════════════════╝

🔍 Checking RuleService connectivity...
✅ RuleService is healthy

🗑️  Deleting existing rules...
   ✓ Deleted rule: rule-weight-standard

📝 Creating new rules from data/rules.json...
   ✅ Created: Standard Weight Pricing (ID: rule-weight-standard)
   ✅ Created: Lunch Hour Discount (ID: rule-promo-lunch)
   ✅ Created: Early Bird Discount (ID: rule-promo-morning)
   ✅ Created: Southern Remote Zone (ID: rule-surcharge-south)
   ✅ Created: Northeast Remote Zone (ID: rule-surcharge-northeast)

✨ Sample rules loaded successfully!
```

---

### 2. `bulk-quotes.sh` — Submit Bulk Quote Job

สร้างจากข้อมูลใน CSV แล้วรอการประมวลผลจากระบบ

**Usage:**

```bash
# Basic (uses default CSV and URL)
./scripts/bulk-quotes.sh

# With custom CSV
./scripts/bulk-quotes.sh data/bulk_quotes.csv

# With custom URL
./scripts/bulk-quotes.sh data/bulk_quotes.csv http://localhost:5001
```

**CSV Format:**

```csv
weightKg,originZip,destinationZip,description
3,10100,10200,Bangkok to Nearby
12,10100,95120,Bangkok to Remote Area
...
```

**What it does:**

1. ✅ ตรวจสอบ PricingService connectivity
2. 📋 Parse CSV file
3. 📤 Submit bulk quote request
4. ⏳ Poll for job completion
5. 📊 Display results

**Example Output:**

```
╔═══════════════════════════════════════════════════════════════╗
║          Submitting Bulk Quotes                              ║
║          CSV: data/bulk_quotes.csv
║          URL: http://localhost:5001
╚═══════════════════════════════════════════════════════════════╝

🔍 Checking PricingService connectivity...
✅ PricingService is healthy

📋 Preparing bulk quote request...
   ✓ Added: Bangkok_to_Nearby (3kg)
   ✓ Added: Bangkok_to_Remote_Area (12kg)

📤 Submitting 10 quotes...

✅ Bulk quote submitted successfully

🆔 Job ID: 550e8400-e29b-41d4-a716-446655440000
📊 Status: pending

⏳ Waiting for processing to complete...
⏳ Status: completed    | Results: 10/10

✅ All quotes processed successfully!

📊 Results Summary:
{
  "basePrice": 60,
  "discount": 9,
  "surcharge": 0,
  "finalPrice": 51
}
```

---

## Quick Start Workflow

### 1️⃣ Start Services

```bash
# Terminal 1
cd RuleService && dotnet run --launch-profile http

# Terminal 2
cd PricingService && dotnet run --launch-profile http
```

Or with Docker:

```bash
docker compose up --build
```

### 2️⃣ Seed Rules

```bash
./scripts/seed-rules.sh
```

### 3️⃣ Test Single Quote (via Scalar UI)

Open browser: http://localhost:5001/scalar/v1

- Find `POST /quotes/price`
- Input: `{"weightKg": 12, "originZip": "10100", "destinationZip": "95120"}`
- Click **Send**

### 4️⃣ Submit Bulk Quotes

```bash
./scripts/bulk-quotes.sh
```

---

## Data Files

### `data/rules.json`

Sample rules with 5 examples:

- **WeightTierRule** — Standard pricing by weight tier
- **TimeWindowPromotionRule** — Lunch & Early Bird discounts
- **RemoteAreaSurchargeRule** — Southern & Northeast surcharges

### `data/bulk_quotes.csv`

10 sample quote requests covering:

- ✅ Normal area + Normal weight
- ✅ Remote area + Heavy weight
- ✅ Multiple rule combinations

---

## Prerequisites

- `bash` shell
- `curl` (for HTTP requests)
- `jq` (for JSON parsing)

**Install jq (if not available):**

```bash
# macOS
brew install jq

# Ubuntu/Debian
sudo apt-get install jq

# CentOS/RHEL
sudo yum install jq
```

---

## Troubleshooting

### ❌ "Cannot connect to RuleService"

**Solution:** Make sure RuleService is running on port 5002

```bash
curl http://localhost:5002/health
```

### ❌ "CSV file not found"

**Solution:** Run scripts from project root directory

```bash
cd /path/to/erp
./scripts/bulk-quotes.sh
```

### ❌ "jq: command not found"

**Solution:** Install jq using package manager (see Prerequisites)

### ❌ Scripts are not executable

**Solution:** Make scripts executable

```bash
chmod +x scripts/*.sh
```

---

## Customization

### Modify Sample Rules

Edit `data/rules.json` to add/change rules:

```json
{
  "$type": "WeightTier",
  "name": "Custom Pricing",
  "enabled": true,
  "tiers": [
    { "minKg": 0, "maxKg": 10, "pricePerKg": 25 },
    { "minKg": 10.01, "maxKg": 50, "pricePerKg": 20 }
  ]
}
```

Then re-run:

```bash
./scripts/seed-rules.sh
```

### Add More Bulk Quote Samples

Edit `data/bulk_quotes.csv`:

```csv
weightKg,originZip,destinationZip,description
15,40000,95200,Chiang Mai to Phatthalung
...
```

Then submit:

```bash
./scripts/bulk-quotes.sh
```

---

## Next Steps

📖 Read [Main README](../README.md) for full project documentation

🌐 Open Scalar UI for interactive API testing: http://localhost:5001/scalar/v1

🧪 Run unit tests: `dotnet test`
