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
- أعمدة بتوزيع نجمي:
  - اسم البند (4*)
  - اسم الشركة (2*)
  - اسم الموظف (2*)
  - الكمية (1*)
  - السعر (1.5*)
  - الإجمالي (1.5*)
  - المرفقات (1.5*)
  - إجراءات (80px ثابت)
- حساب تلقائي للإجمالي
- أزرار إضافة، حفظ، وحذف

### البنية المعمارية
- **MVVM Pattern**: فصل كامل بين UI و Business Logic
- **CommunityToolkit.Mvvm**: لتسهيل تطبيق MVVM
- **Data Binding**: ربط بيانات ثنائي الاتجاه
- **ObservableCollection**: لتحديث واجهة المستخدم تلقائياً

### الملفات الرئيسية
- `Views/LoginPage.xaml` - واجهة تسجيل الدخول
- `Views/TransactionPage.xaml` - واجهة المعاملات
- `ViewModels/LoginViewModel.cs` - منطق تسجيل الدخول
- `ViewModels/TransactionViewModel.cs` - منطق المعاملات
- `Models/TransactionItem.cs` - نموذج بيانات البند
