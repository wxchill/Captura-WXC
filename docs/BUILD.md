# 构建

使用 Windows PowerShell 7、.NET Framework 4.x 的 C# 编译器以及 Inno Setup 7（生成安装包时需要）。

```powershell
# 只构建发布运行文件，使用仓库内的轻量快照
./build.ps1

# 先把已发布的完整安装包解压到一个空目录，不写系统卸载项、快捷方式或个人配置
./Captura-WXC-2026.11.06.1-Setup-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /EXTRACTONLY=1 /DIR="C:\Build\FullRuntime" /NOICONS /TASKS=""

# 使用完整运行文件重建安装包（提供微软签名的 VC++ x64 运行库安装文件）
./build.ps1 -RuntimeDirectory 'C:\Build\FullRuntime' -Setup -InnoCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe' -VCRedist 'C:\Build\VC_redist.x64.exe'
```

小快照不含 FFmpeg 和模型，不可直接作为完整安装包分发。完整运行文件应来自本仓库对应 Release。原始开发机安装目录及个人设置不会被脚本读取。

构建脚本编译桌宠、截图编辑器、桥接、单实例、保存对话框、录制状态、录制按钮和发布引导，然后对快照主程序进行受检查的接入补丁。FFmpeg 的路径修复只修改查找逻辑，保留已存在的编码参数修复。

保存对话框、录制状态和分离按钮的初始编辑源码缺失，相应源码从本次现有程序集使用 ICSharpCode.Decompiler 9.1.0.7988 恢复，再做保存安全性修复；文件级命名空间降为 C# 5 兼容语法。原始程序集版本号保留。快照模式意味着这不是从零构建所有上游 DLL 的流水线，后续可逐步将二进制接入迁移回上游项目源码。

`packaging/src/ReleaseChecks.cs` 验证首次配置、已有配置保留、编码器路径修复、损坏配置留存、目标文件被锁时原文件保留和跨卷另存。`extensions/desktop-pet/Checks.cs` 验证桌宠状态、时钟清零、配置同步和布局稳定。

素材 `extensions/desktop-pet/assets/recording-device-final.png` 使用生成式图像工具制作，提示词随素材保留；眼睛、计时数字和按钮为代码绘制。

第三方来源：

- Captura 8.0.0：https://github.com/MathewSachin/Captura/tree/v8.0.0
- Mono.Cecil 0.11.x：https://github.com/jbevain/cecil
- Inno Setup：https://jrsoftware.org/isdl.php
- Microsoft VC++ x64：https://aka.ms/vc14/vc_redist.x64.exe
- FFmpeg 9.0.2 Gyan essentials：https://www.gyan.dev/ffmpeg/builds/ ，对应 FFmpeg 提交 `946fcce07b`，构建配置及依赖版本见安装目录 `licenses/FFmpeg/README.txt`。
- 英译中模型：https://argos-net.com/v1/translate-en_zh-1_9.argosmodel ，SHA256 `433e7c4f034d87fbe2353161e05f18646d7999452f801a4e1f0378522b9850ab`。
- Python、OCR、CTranslate2 和各依赖的许可证随完整运行文件保留。
