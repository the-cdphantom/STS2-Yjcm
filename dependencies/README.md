# 双版本编译依赖

仅用于本机编译与回归，不随模组或 GitHub 源码分发游戏 DLL。

- stable-0.111.0：固定稳定 API 快照。
- beta-0.114.0：固定测试 API 快照。
- manifest.json：可公开的文件名和 SHA-256，不包含个人来源路径。
- GameReferences.props：统一业务工程和测试工程的引用入口。

开发者应合法取得匹配文件，然后运行：

```powershell
pwsh ./tools/prepare-dependencies.ps1 -Variant stable -SourceDirectory 'C:/Local/stable-snapshot'
pwsh ./tools/prepare-dependencies.ps1 -Variant beta -SourceDirectory 'C:/Local/beta-snapshot'
pwsh ./tools/build.ps1 -Variant all
```

不会自动下载游戏 DLL，不会跟随 Steam 当前分支，也不依赖旧工程的缓存。升级游戏 API 时新增经校验的快照并完成两版适配/回归后再更新映射。
