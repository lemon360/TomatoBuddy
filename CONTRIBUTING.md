# 参与贡献

欢迎通过 Issues 报告问题或提出建议，通过 Pull Request 提交修改。

请提供 Windows 版本、显示缩放比例、显示器数量和复现步骤。不要上传个人专注记录、令牌或其他私人信息。

开发环境：Windows + .NET 8 SDK。提交前运行：

```powershell
dotnet run --project Tests/Tests.csproj -c Release
dotnet build TomatoBuddy.csproj -c Release
```

修改强制休息功能时，应验证紧急退出、自动解除、普通关闭保护和多显示器行为。保留系统安全操作，不引入无法退出的系统限制。
