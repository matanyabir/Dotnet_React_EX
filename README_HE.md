# 💡 מערכת תמיכה - Full-Stack Exercise

מערכת תמיכה מלאה עם Backend ב-ASP.NET Core Minimal API ו-Frontend ב-React.

## 📋 תוכן עניינים

- [מבנה הפרויקט](#מבנה-הפרויקט)
- [התקנה והרצה](#התקנה-והרצה)
- [פיצ'רים](#פיצרים)
- [הגדרות](#הגדרות)
- [API Endpoints](#api-endpoints)

## 🏗️ מבנה הפרויקט

```
.
├── Backend/              # ASP.NET Core Minimal API
│   ├── Models/          # מודלים (Ticket)
│   ├── DTOs/            # Data Transfer Objects
│   ├── Services/        # שירותים (Ticket, Auth, Email, AI)
│   ├── Endpoints/       # API Endpoints
│   ├── Data/            # קבצי JSON (tickets.json)
│   └── Properties/      # הגדרות הפרויקט
│
└── Frontend/            # React Application
    └── src/
        ├── pages/       # דפים (Login, TicketsList, TicketDetail)
        ├── components/  # קומפוננטות (NewTicketModal, PrivateRoute)
        ├── services/    # שירותי API
        └── context/     # Context API (AuthContext)
```

## 🚀 התקנה והרצה

### דרישות מוקדמות

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (גרסה 18 ומעלה)
- [npm](https://www.npmjs.com/) או [yarn](https://yarnpkg.com/)

### Backend

1. פתח טרמינל בתיקיית `Backend`
2. הפעל את הפקודות הבאות:

```bash
cd Backend
dotnet restore
dotnet run
```

השרת ירוץ על `http://localhost:5000`

**הערה:** Swagger UI זמין ב-`http://localhost:5000/swagger`

### Frontend

1. פתח טרמינל חדש בתיקיית `Frontend`
2. הפעל את הפקודות הבאות:

```bash
cd Frontend
npm install
npm run dev
```

האפליקציה תרוץ על `http://localhost:3000`

## ✨ פיצ'רים

### פיצ'רים בסיסיים ✅

- **יצירת כרטיסי תמיכה** - כל משתמש יכול ליצור כרטיס חדש
- **צפייה בכל הכרטיסים** - רשימה עם טבלה מסודרת
- **צפייה בכרטיס ספציפי** - לפי ID ייחודי
- **עריכת כרטיסים** - רק למשתמשים מחוברים (Admin)
- **פילטרים** - לפי סטטוס וחיפוש טקסט
- **שמירת נתונים** - ב-JSON file בצד השרת

### פיצ'רים בונוס ✅

- **Authentication & JWT** - התחברות מאובטחת עם JWT Token
- **AI Summary** - יצירת סיכום אוטומטי של הבעיה (תמיכה ב-OpenAI/Gemini)
- **Email Notifications** - שליחת אימיילים (סימולציה או SMTP אמיתי)

## 🔐 משתמשים לדוגמה

המערכת מגיעה עם שני משתמשים מוגדרים מראש:

| שם משתמש | סיסמה | תפקיד |
|----------|-------|-------|
| `admin` | `admin123` | מנהל |
| `user` | `user123` | משתמש |

**הערה:** רק משתמשים מחוברים יכולים לערוך כרטיסים. כל אחד יכול ליצור כרטיסים חדשים.

## ⚙️ הגדרות

### Email (אופציונלי)

ערוך את `Backend/appsettings.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@gmail.com",
    "SmtpPassword": "your-app-password"
  }
}
```

**הערה:** עבור Gmail, יש ליצור "App Password" בהגדרות החשבון.

אם לא מוגדר, המערכת תדמה שליחת אימיילים (תדפיס לקונסול).

### AI (אופציונלי)

ערוך את `Backend/appsettings.json`:

```json
{
  "AI": {
    "ApiKey": "your-openai-api-key",
    "ApiUrl": "https://api.openai.com/v1/chat/completions",
    "Model": "gpt-3.5-turbo"
  }
}
```

**הערה:** אם לא מוגדר API Key, המערכת תחזיר סיכום בסיסי (50 תווים ראשונים).

## 📝 API Endpoints

### Tickets

| Method | Endpoint | תיאור | Auth |
|--------|----------|-------|------|
| `GET` | `/api/tickets` | קבלת כל הכרטיסים | ❌ |
| `GET` | `/api/tickets?status=Open&search=text` | פילטרים | ❌ |
| `GET` | `/api/tickets/{id}` | קבלת כרטיס לפי ID | ❌ |
| `POST` | `/api/tickets` | יצירת כרטיס חדש | ❌ |
| `PUT` | `/api/tickets/{id}` | עדכון כרטיס | ✅ |

### Auth

| Method | Endpoint | תיאור |
|--------|----------|-------|
| `POST` | `/api/auth/login` | התחברות |
| `GET` | `/api/auth/validate` | בדיקת תקינות Token |

### דוגמאות

#### יצירת כרטיס חדש

```bash
POST http://localhost:5000/api/tickets
Content-Type: application/json

{
  "fullName": "יוסי כהן",
  "email": "yossi@example.com",
  "description": "המחשב שלי לא נדלק"
}
```

#### התחברות

```bash
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

#### עדכון כרטיס (דורש Token)

```bash
PUT http://localhost:5000/api/tickets/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "status": "Resolved",
  "resolution": "הבעיה נפתרה על ידי איפוס המחשב"
}
```

## 🎨 UI/UX

- **עיצוב מודרני** - גרדיאנטים וצבעים נעימים
- **רספונסיבי** - עובד על כל המכשירים
- **תמיכה בעברית** - RTL מלא
- **מודלים** - חלונות קופצים מסודרים
- **הודעות** - שגיאה והצלחה ברורות
- **אינדיקטורי טעינה** - משוב למשתמש

## 📧 Email Notifications

המערכת שולחת אימיילים (או מדמה שליחה) במקרים הבאים:

1. **לאחר יצירת כרטיס חדש** - אימייל ללקוח עם קישור למעקב
2. **כאשר הסטטוס משתנה** - עדכון על שינוי סטטוס
3. **כאשר הפתרון מתעדכן** - הודעה על פתרון חדש

## 🤖 AI Summary

כאשר נוצר כרטיס חדש, המערכת מנסה ליצור סיכום AI של הבעיה:

- **עם API Key** - קריאה ל-OpenAI API ליצירת סיכום חכם
- **ללא API Key** - סיכום בסיסי (50 תווים ראשונים)

הסיכום נוצר באופן אסינכרוני ולא חוסם את יצירת הכרטיס.

## 🛠️ טכנולוגיות

### Backend
- ASP.NET Core 8.0
- Minimal API
- JWT Authentication
- BCrypt (הצפנת סיסמאות)
- MailKit (שליחת אימיילים)
- Newtonsoft.Json (עבודה עם JSON)

### Frontend
- React 18
- React Router DOM
- Axios (HTTP Client)
- Vite (Build Tool)

## 📄 רישיון

פרויקט זה נוצר כחלק מתרגיל Full-Stack.

## 🐛 פתרון בעיות

### Backend לא רץ
- ודא ש-.NET 8.0 מותקן: `dotnet --version`
- ודא שהפורט 5000 פנוי

### Frontend לא רץ
- ודא ש-Node.js מותקן: `node --version`
- הפעל `npm install` שוב

### CORS Errors
- ודא שה-Backend רץ על `http://localhost:5000`
- ודא שה-Frontend רץ על `http://localhost:3000`

### Authentication לא עובד
- ודא שה-Token נשמר ב-localStorage
- בדוק את ה-Console לדיבוג

---

**בהצלחה! 🚀**

