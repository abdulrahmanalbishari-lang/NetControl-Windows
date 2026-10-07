# NetControl Windows

هذه النسخة مبنية من مشروع Android المرفوع كمصدر مرجعي. تم نقل البنية والموارد الأساسية إلى WPF/.NET 8، مع RouterOS API مباشر وواجهة Windows أصلية وWorkflow للبناء على GitHub Actions.

مهم: ملفات Android الأصلية ليست واجهات Windows قابلة للتشغيل حرفيًا؛ الوظائف التي تعتمد على Activity/Intent/Android UI تحتاج إعادة تنفيذ بمكافئ Windows. هذه النسخة تتجنب WebView وتضع طبقة RouterOS وواجهة الوحدات كأساس قابل للتوسعة.
