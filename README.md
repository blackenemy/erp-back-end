# Mini Pricing Platform

ระบบคำนวณราคาขนส่งแบบ microservices ประกอบด้วย 2 services ที่สื่อสารกันผ่าน HTTP

## Architecture

```
┌─────────────────┐       GET /rules        ┌─────────────────┐
│  PricingService  │ ─────────────────────▶  │   RuleService    │
│   (port 5001)    │ ◀─────────────────────  │   (port 5002)    │
└────────┬─────────┘      List<Rule>         └────────┬─────────┘
         │                                            │
    jobs.json                                    rules.json
```

## Services

### RuleService (port 5002)

CRUD สำหรับกฎการคิดราคา 3 ประเภท:

| Rule                        | หน้าที่                                       |
| --------------------------- | --------------------------------------------- |
| **WeightTierRule**          | กำหนดราคาตามช่วงน้ำหนัก เช่น 0–5 kg = 10 ฿/kg |
| **TimeWindowPromotionRule** | ลดราคาตามช่วงเวลา เช่น 10:00–14:00 ลด 15%     |
| **RemoteAreaSurchargeRule** | บวกค่าส่งพื้นที่ห่างไกลตาม zip prefix         |

**Endpoints:**

| Method | Path          | Description  |
| ------ | ------------- | ------------ |
| GET    | `/health`     | Health check |
| GET    | `/rules`      | ดึงกฎทั้งหมด |
| GET    | `/rules/{id}` | ดึงกฎตาม ID  |
| POST   | `/rules`      | สร้างกฎใหม่  |
| PUT    | `/rules/{id}` | แก้ไขกฎ      |
| DELETE | `/rules/{id}` | ลบกฎ         |

### PricingService (port 5001)

คำนวณราคาจากกฎที่ดึงมาจาก RuleService รองรับ 2 โหมด:

**Single Quote** — `POST /quotes/price`

ส่ง `QuoteRequest` แล้วได้ `QuoteResult` กลับทันที

**Bulk Quote (async)** — `POST /quotes/bulk`

ส่ง list ของ `QuoteRequest` → ได้ `jobId` กลับทันที (202 Accepted) → Background worker ประมวลผลผ่าน `Channel<T>` → Poll สถานะที่ `GET /jobs/{jobId}`

```
pending → processing → completed | failed
```

**Endpoints:**

| Method | Path            | Description                 |
| ------ | --------------- | --------------------------- |
| GET    | `/health`       | Health check                |
| POST   | `/quotes/price` | คำนวณราคาเดี่ยว             |
| POST   | `/quotes/bulk`  | คำนวณราคาแบบ bulk (async)   |
| GET    | `/jobs/{jobId}` | ดูสถานะ/ผลลัพธ์ของ bulk job |

## Rule Types

กฎทั้ง 3 ประเภทสืบทอดจาก `Rule` base class และใช้ `$type` discriminator ในการแยกประเภทตอน serialize/deserialize (polymorphic JSON)

### WeightTierRule — คิดราคาตามน้ำหนัก

กำหนดช่วงน้ำหนัก (tier) พร้อมราคาต่อ kg แต่ละช่วง ระบบจะหา tier ที่ตรงกับน้ำหนักของ request แล้วคำนวณเป็น **basePrice**

```json
{
  "$type": "WeightTier",
  "name": "Standard Weight Pricing",
  "type": "WeightTier",
  "enabled": true,
  "tiers": [
    { "minKg": 0, "maxKg": 5, "pricePerKg": 10 },
    { "minKg": 5, "maxKg": 20, "pricePerKg": 8 },
    { "minKg": 20, "maxKg": 100, "pricePerKg": 6 }
  ]
}
```

ตัวอย่าง: ของหนัก 12 kg → ตรง tier 5–20 kg → `basePrice = 12 × 8 = 96 ฿`

### TimeWindowPromotionRule — ลดราคาตามช่วงเวลา

กำหนดช่วงเวลา (HH:mm) และ % ส่วนลด ถ้าเวลาปัจจุบันอยู่ในช่วง จะคิด **discount** เป็น % ของ basePrice

```json
{
  "$type": "TimeWindowPromotion",
  "name": "Lunch Promo",
  "type": "TimeWindowPromotion",
  "enabled": true,
  "startTime": "11:00",
  "endTime": "13:00",
  "discountPercent": 15
}
```

ตัวอย่าง: สั่งตอน 12:30 + basePrice 96 ฿ → `discount = 96 × 15% = 14.4 ฿`

### RemoteAreaSurchargeRule — บวกค่าส่งพื้นที่ห่างไกล

กำหนด list ของ zip prefix ที่ถือว่าเป็นพื้นที่ห่างไกล พร้อมค่าบวกเพิ่มแบบ flat ถ้า destinationZip ขึ้นต้นด้วย prefix ที่กำหนด จะบวก **surcharge**

```json
{
  "$type": "RemoteAreaSurcharge",
  "name": "Remote Area Fee",
  "type": "RemoteAreaSurcharge",
  "enabled": true,
  "remoteZipPrefixes": ["95", "96", "63"],
  "surchargeFlat": 30
}
```

ตัวอย่าง: ส่งไป zip 95120 → ขึ้นต้นด้วย "95" → `surcharge = 30 ฿`

### สรุปการคำนวณ

กฎทั้ง 3 ประเภททำงานร่วมกัน โดยแต่ละตัวรับผิดชอบคนละส่วนของราคาสุดท้าย:

```text
BasePrice   = WeightTierRule (น้ำหนัก × ราคาต่อ kg ตาม tier)
Discount    = TimeWindowPromotionRule (% ของ BasePrice ถ้าอยู่ในช่วงเวลา)
Surcharge   = RemoteAreaSurchargeRule (ค่าบวกเพิ่มถ้า zip ตรง)

FinalPrice  = BasePrice − Discount + Surcharge
```

จากตัวอย่างทั้งหมดข้างบน: `FinalPrice = 96 − 14.4 + 30 = 111.6 ฿`

ถ้าไม่มีกฎ WeightTier ตรง จะใช้ Base Flat Rate = 50 ฿

## Quote Request Flow

เมื่อเรียก `POST /quotes/price` ระบบรับ `QuoteRequest`:

```json
{
  "weightKg": 12,
  "originZip": "10100",
  "destinationZip": "95120"
}
```

จากนั้น PricingService จะดึงกฎ **ทั้งหมด** จาก RuleService (`GET /rules`) แล้ว `PricingEngine` วน loop กฎที่ `enabled: true` ทุกตัว:

```text
QuoteRequest เข้ามา
       │
       ▼
ดึง rules ทั้งหมดจาก RuleService
       │
       ▼
วน loop แต่ละ rule ที่ enabled
       │
       ├─ WeightTierRule:  เอา weightKg ไปจับคู่กับ tier
       │   12 kg ตรง tier 5-20 → basePrice = 12 × 8 = 96
       │
       ├─ TimeWindowPromotionRule:  เช็คเวลาปัจจุบันของ server
       │   ถ้าอยู่ในช่วง 11:00-13:00 → discount = 96 × 15% = 14.4
       │
       └─ RemoteAreaSurchargeRule:  เอา destinationZip เทียบ prefix
           "95120" ขึ้นต้นด้วย "95" → surcharge = 30
       │
       ▼
FinalPrice = basePrice - discount + surcharge
```

### Field ไหนอ้างอิงกฎไหน

| Field ใน Request    | กฎที่ใช้                | ตรวจสอบอะไร                                                                      |
| ------------------- | ----------------------- | -------------------------------------------------------------------------------- |
| `weightKg`          | WeightTierRule          | จับคู่กับ `minKg`/`maxKg` ใน tiers                                               |
| `destinationZip`    | RemoteAreaSurchargeRule | เช็คว่าขึ้นต้นด้วย `remoteZipPrefixes` ไหม                                       |
| _(ไม่ได้ใช้ field)_ | TimeWindowPromotionRule | เช็ค **เวลาปัจจุบันของ server** เทียบ `startTime`/`endTime` (ดูหมายเหตุด้านล่าง) |
| `originZip`         | ยังไม่มีกฎใช้           | สำรองไว้สำหรับกฎในอนาคต                                                          |

> **หมายเหตุ:** ไม่มีการ "เลือก" ว่าจะใช้กฎไหน — กฎทุกตัวที่ enabled จะถูกประเมินทุกครั้ง ถ้าเงื่อนไขตรงก็มีผล ถ้าไม่ตรงก็ข้ามไป

> **เรื่อง TimeWindowPromotion:** `startTime` / `endTime` เป็น field ของ **กฎ** ไม่ใช่ของ request — client ไม่ต้องส่งเวลามา ระบบดึงเวลาปัจจุบันจาก server เอง (`TimeProvider.System.GetLocalNow()`) แล้วเทียบกับช่วงเวลาที่กำหนดไว้ในกฎ

### พื้นที่ห่างไกลวัดจากอะไร

`RemoteAreaSurchargeRule` **ไม่ได้คำนวณระยะทางจริง** — เป็น business rule ที่กำหนดเองว่า zip prefix ไหนเป็นพื้นที่ห่างไกล โดยเช็คว่า `destinationZip` ขึ้นต้นด้วย prefix ที่อยู่ใน `remoteZipPrefixes` หรือไม่:

```text
remoteZipPrefixes: ["95", "96", "63"]

zip 95120  → ขึ้นต้นด้วย "95" → พื้นที่ห่างไกล → บวก surcharge
zip 10200  → ไม่ตรง prefix ไหน → พื้นที่ปกติ   → ไม่บวก
```

ต้องการเพิ่ม/ลด prefix ก็แก้ที่ rule ผ่าน `PUT /rules/{id}` ได้เลย

## Tech Stack

- **.NET 10** — Minimal APIs + OpenAPI/Scalar
- **Shared project** — โมเดลที่ใช้ร่วมกัน พร้อม source-generated JSON (AOT-friendly)
- **Channel & BackgroundService** — async bulk processing
- **ConcurrentDictionary + JSON file** — in-memory store พร้อม persistence
- **Scalar** — Modern API documentation UI (alternative to Swagger)
- **Structured Logging** — Console logging with configurable levels
- **Docker Compose** — Multi-container orchestration
- **CORS** — Cross-Origin Resource Sharing enabled for local development

## Getting Started

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose
- curl or Postman (สำหรับ testing)

### Run with Docker Compose

```bash
# Build and start services
docker compose up --build

# View logs
docker compose logs -f

# Stop services
docker compose down
```

### Run Locally (Development)

```bash
# Terminal 1 — RuleService
cd RuleService
dotnet run --launch-profile http

# Terminal 2 — PricingService
cd PricingService
dotnet run --launch-profile http
```

### Environment Configuration

Copy `.env.example` to `.env` and customize:

```bash
cp .env.example .env
```

**Key variables:**

| Variable                 | Default               | Description                                               |
| ------------------------ | --------------------- | --------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT` | Development           | Environment mode                                          |
| `LOG_LEVEL`              | Information           | Logging level (Trace, Debug, Information, Warning, Error) |
| `STRUCTURED_LOGGING`     | false                 | Enable JSON-formatted logs                                |
| `RULE_SERVICE_URL`       | http://localhost:5002 | RuleService address                                       |
| `SHOW_DETAILED_ERRORS`   | true                  | Display error details in responses                        |

### API Documentation — Scalar UI

**Scalar** is a modern, interactive API documentation UI that replaces traditional Swagger UI.

#### Access Scalar UI

- **PricingService**: http://localhost:5001/scalar/v1
- **RuleService**: http://localhost:5002/scalar/v1

#### Features

✨ **Interactive Testing**

- Try API endpoints directly from the browser
- See real-time request/response
- Test different HTTP methods

🔄 **OpenAPI Schema Integration**

- Automatically generated from .NET minimal APIs
- Full endpoint documentation
- Request/response schema visualization

📦 **Request/Response Examples**

- Copy-paste ready curl commands
- JSON payload templates
- Response status codes

🎨 **Modern UI**

- Clean, user-friendly design
- Dark mode support
- Search functionality
- Keyboard shortcuts

#### How to Use Scalar

1. Open http://localhost:5001/scalar/v1 in your browser
2. Browse endpoints in the left sidebar
3. Click on any endpoint to expand details
4. Fill in parameters/body in the request panel
5. Click **Send** to execute
6. View response in the right panel
7. Use **Copy curl** to get command-line equivalent

#### Example with Scalar

```
1. Open: http://localhost:5001/scalar/v1
2. Find: POST /quotes/price
3. Click on endpoint
4. Input body:
   {
     "weightKg": 12,
     "originZip": "10100",
     "destinationZip": "95120"
   }
5. Click Send → See response with calculated price
```

### Logging Configuration

Services support configurable logging levels via environment variables.

**Local development** (console output):

```bash
# Terminal output
export LOG_LEVEL=Debug
dotnet run
```

**Docker** (structured JSON logs):

```yaml
# docker-compose.yml excerpt
environment:
  - STRUCTURED_LOGGING=true
  - LOG_LEVEL=Information
```

**View logs:**

```bash
# Follow logs from all services
docker compose logs -f

# Follow logs from specific service
docker compose logs -f ruleservice

# See last 50 lines
docker compose logs --tail=50
```

### Quick Test

```bash
# Health check
curl http://localhost:5001/health
curl http://localhost:5002/health

# สร้างกฎ WeightTier
curl -X POST http://localhost:5002/rules \
  -H "Content-Type: application/json" \
  -d '{
    "$type": "WeightTier",
    "name": "Standard Weight Pricing",
    "type": "WeightTier",
    "enabled": true,
    "tiers": [
      { "minKg": 0, "maxKg": 5, "pricePerKg": 10 },
      { "minKg": 5, "maxKg": 20, "pricePerKg": 8 },
      { "minKg": 20, "maxKg": 100, "pricePerKg": 6 }
    ]
  }'

# คำนวณราคา
curl -X POST http://localhost:5001/quotes/price \
  -H "Content-Type: application/json" \
  -d '{ "weightKg": 12, "originZip": "10100", "destinationZip": "10200" }'
```

## Project Structure

```
erp/
├── docker-compose.yml
├── Shared/              # Shared models & JSON context
│   └── Class1.cs
├── data/                # Sample data files
│   ├── rules.json       # Sample rules (5 examples)
│   └── bulk_quotes.csv  # Sample bulk quote requests (10 examples)
├── scripts/             # Helper scripts for seeding & testing
│   ├── seed-rules.sh    # Seed sample rules from data/rules.json
│   ├── bulk-quotes.sh   # Submit bulk quotes from CSV
│   └── README.md        # Scripts documentation
├── PricingService/      # ราคาคำนวณ + bulk job processing
│   ├── Program.cs
│   └── Dockerfile
└── RuleService/         # CRUD กฎการคิดราคา
    ├── Program.cs
    └── Dockerfile
```
