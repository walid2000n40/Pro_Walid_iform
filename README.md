# Pro Walid - نظام إدارة المعاملات

## WinUI 3 Application with MVVM Architecture

### المتطلبات
- .NET 8.0 SDK
- Windows App SDK 1.5
- Visual Studio 2022 (مع workload: .NET Desktop Development و Universal Windows Platform Development)

### البناء والتشغيل

```powershell
# استعادة الحزم
dotnet restore

# بناء المشروع
dotnet build

# تشغيل التطبيق
dotnet run
```

### الميزات

#### صفحة تسجيل الدخول
- تصميم حديث مع بطاقة في المنتصف
- خلفية بتدرج لوني جميل
- حقول اسم المستخدم وكلمة المرور
- زر تسجيل دخول مع تأثيرات Hover
- بيانات الدخول الافتراضية: `admin` / `admin`

#### صفحة المعاملات
- جدول كامل العرض مُحسّن لشاشة 27 بوصة
- عرض تفاصيل البنود من جميع المعاملات
- أعمدة بتوزيع نجمي قابل للتعديل:
  - بند الخدمة (3*)
  - العدد (*)
  - سعر الوحدة (1.5*)
  - الإجمالي (1.5*)
  - اسم الشركة (3*)
  - اسم الموظف (2*)
  - المرفقات (1.5*)
- حساب تلقائي للإجمالي
- أزرار إضافة معاملة، تعديل معاملة، وحذف

#### صفحة إضافة/تعديل معاملة
- نافذة منفصلة لإدارة المعاملات
- معلومات المعاملة: رقم الفاتورة (تلقائي)، الشركة، الموظف، التاريخ
- إضافة بنود متعددة لكل معاملة
- حساب تلقائي للإجماليات
- دعم المرفقات لكل بند

### البنية المعمارية
- **MVVM Pattern**: فصل كامل بين UI و Business Logic
- **CommunityToolkit.Mvvm**: لتسهيل تطبيق MVVM
- **Data Binding**: ربط بيانات ثنائي الاتجاه
- **ObservableCollection**: لتحديث واجهة المستخدم تلقائياً

### الملفات الرئيسية
- `Views/LoginPage.xaml` - واجهة تسجيل الدخول
- `Views/TransactionPage.xaml` - واجهة المعاملات
- `Views/AddTransactionPage.xaml` - واجهة إضافة/تعديل معاملة
- `ViewModels/LoginViewModel.cs` - منطق تسجيل الدخول
- `ViewModels/TransactionViewModel.cs` - منطق المعاملات
- `ViewModels/AddTransactionPageViewModel.cs` - منطق إضافة/تعديل معاملة
- `Models/Transaction.cs` - نموذج المعاملة الكاملة
- `Models/TransactionItemDetail.cs` - نموذج بيانات البند
- `Models/TransactionItemWithDetails.cs` - نموذج البند مع معلومات المعاملة
