# Asset Inventory — KSAUHS Enterprise

نظام إدارة أصول مؤسسي متكامل لمستشفى الملك سعود الجامعي (KSAUHS).
تطبيق سطح مكتب Windows بديل احترافي لـ Excel مع:

## المميزات

- **تعديل مباشر في الجدول** — بدون نوافذ منبثقة، مثل Excel تماماً
- **لوحة تحليلات** — رسم بياني دائري + أعمدة أفقية + إحصاءات فورية
- **5 بطاقات KPI** مع شريط نسبة مرئي
- **Status Badges ملوّنة** — Verified / Pending / Disposed / Transferred
- **تصدير Excel حقيقي (.xlsx)** معألوان وتنسيق احترافي
- **استيراد CSV** مع حماية كاملة من CSV Injection
- **Bulk Status Change** — غيّر حالة عشرات الأصول دفعة واحدة
- **بحث فوري** في رقم TAG، الوصف، الموقع، الملاحظات
- **اختصارات لوحة المفاتيح**: `Ctrl+N` إضافة، `F2` تعديل، `Del` حذف، `Ctrl+F` بحث، `F5` تحديث
- **صفر Telemetry** — لا إرسال بيانات لأي جهة
- **يعمل بدون إنترنت** — SQLite محلي بالكامل

## البناء

### المتطلبات
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Windows 10/11 x64

### تشغيل مباشر
```cmd
dotnet run --project AssetInventoryNode.csproj
```

### بناء EXE مستقل
```cmd
dotnet publish AssetInventoryNode.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

## الحزم المستخدمة

| Package | الغرض |
|---------|--------|
| `Microsoft.Data.Sqlite` | قاعدة بيانات SQLite محلية |
| `ClosedXML` | تصدير Excel (.xlsx) حقيقي |

## الترخيص

MIT License — استخدم وعدّل بحرية.
