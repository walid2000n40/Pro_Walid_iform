# تعليمات البناء - Pro Walid

## المشكلة الحالية
هناك مشكلة في الـ XamlCompiler عند استخدام `dotnet build` من سطر الأوامر. هذه مشكلة معروفة مع WinUI 3 في بعض البيئات.

## الحل الموصى به: استخدام Visual Studio 2022

### الخطوات:

1. **افتح Visual Studio 2022**
   - تأكد من تثبيت workload: **Universal Windows Platform development**
   - تأكد من تثبيت workload: **.NET Desktop Development**

2. **افتح المشروع**
   ```
   File → Open → Project/Solution
   اختر: C:\ProWalid\ProWalid.sln
   ```

3. **استعد الحزم (Restore Packages)**
   ```
   Tools → NuGet Package Manager → Restore NuGet Packages
   ```
   أو انقر بزر الماوس الأيمن على Solution واختر "Restore NuGet Packages"

4. **اختر Platform**
   - في شريط الأدوات العلوي، اختر **x64** (وليس Any CPU)

5. **ابنِ المشروع**
   ```
   Build → Build Solution
   أو اضغط Ctrl+Shift+B
   ```

6. **شغّل التطبيق**
   ```
   Debug → Start Debugging
   أو اضغط F5
   ```

## بيانات تسجيل الدخول
- **اسم المستخدم:** admin
- **كلمة المرور:** admin

## الميزات المتوفرة

### صفحة تسجيل الدخول ✅
- تصميم حديث مع بطاقة مركزية
- خلفية بتدرج لوني أزرق
- حقول اسم المستخدم وكلمة المرور
- زر دخول مع تأثير Hover

### صفحة المعاملات ✅
- جدول كامل العرض (مُحسّن لشاشة 27 بوصة)
- أعمدة بتوزيع نجمي:
  - اسم البند (4*)
  - اسم الشركة (2*)
  - اسم الموظف (2*)
  - الكمية (1*)
  - السعر (1.5*)
  - الإجمالي (1.5*)
  - المرفقات (1.5*)
  - إجراءات (80px)
- حساب تلقائي للإجمالي
- أزرار إضافة، حفظ، وحذف

## البنية المعمارية
- **MVVM Pattern** - فصل كامل بين UI و Logic
- **CommunityToolkit.Mvvm** - لتسهيل MVVM
- **Data Binding** - ربط بيانات ثنائي الاتجاه
- **ObservableCollection** - تحديث تلقائي للواجهة

## حل بديل: إصلاح بيئة dotnet

إذا أردت استخدام `dotnet build`، جرب:

```powershell
# تثبيت Windows App SDK workload
dotnet workload install microsoft-net-sdk-windowsdesktop

# أو إعادة تثبيت .NET SDK
# قم بتحميل أحدث نسخة من: https://dotnet.microsoft.com/download
```

## الملفات الرئيسية

```
C:\ProWalid\
├── Views/
│   ├── LoginPage.xaml          # واجهة تسجيل الدخول
│   ├── LoginPage.xaml.cs
│   ├── TransactionPage.xaml    # واجهة المعاملات
│   └── TransactionPage.xaml.cs
├── ViewModels/
│   ├── LoginViewModel.cs       # منطق تسجيل الدخول
│   └── TransactionViewModel.cs # منطق المعاملات
├── Models/
│   └── TransactionItem.cs      # نموذج بيانات البند
├── App.xaml
├── MainWindow.xaml
└── ProWalid.csproj
```

## ملاحظات مهمة

1. **يجب استخدام x64 Platform** - لا تستخدم Any CPU أو x86
2. **Visual Studio 2022 مطلوب** - WinUI 3 لا يعمل بشكل موثوق مع dotnet CLI
3. **Windows 10 version 1809 أو أحدث** - مطلوب لتشغيل التطبيق

## الدعم

إذا واجهت أي مشاكل:
1. تأكد من تثبيت Visual Studio 2022 مع workloads المطلوبة
2. تأكد من اختيار Platform = x64
3. نظف المشروع (Clean Solution) ثم أعد البناء
4. أعد تشغيل Visual Studio
