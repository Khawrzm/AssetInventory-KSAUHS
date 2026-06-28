# Asset Inventory Enterprise

⚠️ **الحالة / Status:** قيد التطوير النشط (Under Active Development)

👨‍💻 **تطوير وإعداد / Built by:** سليمان الشمري (Sulaiman Alshammari)

---

# 🇸🇦 النسخة العربية (Arabic Version)

نظام إدارة أصول مؤسسي متكامل مصمم كتطبيق سطح مكتب للأنظمة المعتمدة على بيئة Windows، يهدف لتقديم بديل برمي مستقل واحترافي لملفات Excel التقليدية.

## المميزات المستهدفة والجاري تطويرها

- **تعديل مباشر في الجدول** — تجربة تحرير وتعديل لحظية داخل السطور بدون نوافذ منبثقة.
- **لوحة تحليلات مدمجة** — رسوم بيانية دائرية وأعمدة أفقية لتوليد إحصاءات فورية للأصول.
- **مؤشرات الأداء الرقمية** — 5 بطاقات KPI متقدمة مدعومة بشريط مرئي يعكس النسب المئوية.
- **محددات الحالة البصرية** — علامات ملوّنة ذكية لفرز الحالات (Verified / Pending / Disposed / Transferred).
- **محرك تصدير متطور** — تصدير ملفات Excel حقيقية (.xlsx) تحتفظ بالتنسيق والألوان الاحترافية.
- **استيراد آمن** — قراءة ملفات CSV مع فلترة وحماية مدمجة ضد هجمات CSV Injection.
- **التعديل الجماعي (Bulk Change)** — إمكانية تحديث وتغيير حالة عشرات الأصول دفعة واحدة.
- **محرك بحث فوري** — استعلام سريع يبحث متزامناً في رقم الـ TAG، الوصف، الموقع، والملاحظات.
- **دعم اختصارات لوحة المفاتيح** — تسريع العمل عبر: `Ctrl+N` إضافة، `F2` تعديل، `Del` حذف، `Ctrl+F` بحث، `F5` تحديث.
- **خصوصية مطلقة (Zero Telemetry)** — أمان كامل للبيانات؛ لا يتم جمع أو إرسال أي معلومات لأي جهة خارجية.
- **بيئة محلية مستقلة** — يعمل بكفاءة تامة بدون إنترنت بالاعتماد على قاعدة بيانات SQLite محلية.

## البناء والتشغيل التجريبي

### المتطلبات الأساسية
- [.NET 8 SDK](https://microsoft.com)
- نظام تشغيل Windows 10/11 بمعمارية x64

### التشغيل المباشر للتطوير
```cmd
dotnet run --project AssetInventoryNode.csproj
```

### بناء نسخة EXE مستقلة
```cmd
dotnet publish AssetInventoryNode.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

## الحزم البرمجية المستخدمة

| Package | الغرض |
|---------|--------|
| `Microsoft.Data.Sqlite` | إدارة وتخزين البيانات محلياً عبر قاعدة بيانات SQLite |
| `ClosedXML` | توليد ومعالجة ملفات Excel (.xlsx) الاحترافية |

## الترخيص

MIT License — متاح للاستخدام والتعديل الحر.

---

# 🇺🇸 English Version

An enterprise-grade asset management system designed as a native Windows desktop application. It serves as a highly robust, professional alternative to traditional Excel sheets.

## Features Under Development

- **In-Line Grid Editing** — Seamless, cell-direct data manipulation without annoying popups, just like Excel.
- **Analytics Dashboard** — Built-in pie charts, horizontal bar charts, and real-time operational statistics.
- **5 KPI Cards** — Key performance metrics visualization equipped with dynamic percentage progress bars.
- **Color-Coded Status Badges** — Instant visual classification for asset states: Verified, Pending, Disposed, and Transferred.
- **Native Excel Export** — High-fidelity `.xlsx` generation that preserves clean styling, borders, and professional colors.
- **Secure CSV Import** — Robust parsing architecture featuring built-in sanitization to eliminate CSV Injection vectors.
- **Bulk Status Modification** — Efficiency-focused actions to update the operational status of multiple assets concurrently.
- **Instant Global Search** — Highly responsive query mechanism searching across Tag ID, Description, Location, and Notes.
- **Productivity Hotkeys** — Power-user shortcuts: `Ctrl+N` (New), `F2` (Edit), `Del` (Delete), `Ctrl+F` (Search), and `F5` (Refresh).
- **Zero Telemetry** — Absolute data privacy and isolation; no background reporting or structural leaks to foreign servers.
- **Air-Gapped Offline Operation** — Decentralized data storage powered entirely by an embedded local SQLite engine.

## Building and Running

### Prerequisites
- [.NET 8 SDK](https://microsoft.com)
- Windows 10 / 11 (x64 Architecture)

### Run Development Build
```cmd
dotnet run --project AssetInventoryNode.csproj
```

### Publish Standalone Executable (EXE)
```cmd
dotnet publish AssetInventoryNode.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

## Dependencies

| Package | Purpose |
|---------|--------|
| `Microsoft.Data.Sqlite` | Local data persistence via embedded SQLite engine |
| `ClosedXML` | Advanced programmatic Excel (`.xlsx`) layout creation |

## License

MIT License — Feel free to use, modify, and distribute.
