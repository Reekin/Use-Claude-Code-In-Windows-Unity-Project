# Windows下使用CLaude Code开发Unity项目~~指南~~劝退
[English](./README.md)

花了好几个晚上尝试跑通用cc开发Unity项目的流程，简单记录一下遇到的各种问题和解决方式。
结论先放在前面：在当下阶段是不太建议尝试这个流程的——cc不原生支持windows，基本是一步一个坑，而且解决各种工程配置问题的成本都比较高。而且跑通后实际体验也并没有想象中那么惊艳。

基本的安装步骤就是先装wsl，再在wsl里装cc(https://docs.anthropic.com/en/docs/claude-code/setup) 然后在windows开IDE remote 连接到wsl再从那打开windows下的Unity工程。前面的常规和使用步骤我就不作过多介绍了，自己看文档，遇到问题问ai即可。

#踩坑记录

前排提示：最后发现能相对稳定跑通的IDE只有VSCode

1. cursor wsl terminal 启动claude后，/ide指令报找不到ide
  * 需要先在wsl用find找到claude-code.vsix的位置，再进powershell安装它
  * remote wsl 打开windows下的工程目录

2. cursor在用的mcp，claude无法加载
  * 在remote wsl时，ide只认wsl格式的路径，即需要把为claude准备的mcp.json里的路径替换为/mnt/c/开头
  * python脚本，wsl和windows无法共用同一个venv，需要复制一份两边各自用
  * exe程序应该可以直接调用

3. cursor无法正常对项目进行C#语法分析
  * ms官方限制了C#系列插件在非官方版本VSC中的提供，虽然cursor提供了自己的C#插件，但vsc unity插件依赖的还是官方版本的C#
  * 可以尝试cursor C# + dotrush插件，但我没试过，因为我转投Rider了

4. Rider remote wsl后无法正常加载Unity项目，报缺MSBuild
  * 照ms官网在wsl中安装.Net 9.0 SDK，然后在settings - Build, Execution, Deployment -Toolset and Build 中手动将.NET CLI指定为/usr/lib/dotnet/，MSBuild会自动匹配
  * 在wsl中安装mono-complete
    * 装完会导致wsl下无法正常执行exe，需要将以下代码加入wsl的~/.profile内，每次启动wsl时会自动执行一次修正设置
```
# https://github.com/microsoft/WSL/issues/5466
python.exe --version >/dev/null 2>&1 || \
sudo update-binfmts --disable cli
```

5. Rider remote wsl加载Unity项目后无法识别任何符号
  * wsl下的sln和csproj中的文件路径还是windows格式，需要替换为/mnt/c/开头的wsl格式
  * 将以下代码添加至Assets/Editor/目录下，再进Preferences-External Tools重新生成一次项目文件，现在会每次多生成一批wsl专供的项目文件
```
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

public class WSLProjectGenerator : AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
    {
        string wslProjectPath = path.Replace(".csproj", "-wsl.csproj");
        string wslContent = content.Replace(".csproj", "-wsl.csproj");
        wslContent = ConvertToWSLPath(wslContent);
        File.WriteAllText(wslProjectPath, wslContent);
        return content;
    }

    public static string OnGeneratedSlnSolution(string path, string content)
    {
        string wslSolutionPath = path.Replace(".sln", "-wsl.sln");
        string wslContent = content.Replace(".csproj", "-wsl.csproj");
        wslContent = ConvertToWSLPath(wslContent);
        File.WriteAllText(wslSolutionPath, wslContent);
        return content;
    }
    private static string ConvertToWSLPath(string fileContent)
    {
        fileContent = Regex.Replace(fileContent, @"([A-Z]):\\", match => $"/mnt/{match.Groups[1].Value.ToLower()}/");
        return fileContent.Replace("\\", "/");
    }
}
```

6. Rider remote wsl terminal进入claude后无法Esc返回上一级
  * settings-keymap-搜索escape找到Plugins/Terminal/Escape，清除快捷键

7. Rider中调用 ide diagnotstic 失败，始终timeout
  * 正常使用时突然出现这个问题，始终没找到原因，最后转投vscode

8. 无法与unity mcp server正常通讯
  * wsl里的请求被windows防火墙拦截导致，需要找到wsl的ip（ip addr show eth0），在`高级安全Windows Defender防火墙设置`中新建一条入站规则->自定义，允许这个ip的所有访问
  * 但有时候不是立即生效，我还试过netsh advfirewall reset重置防火墙等操作，不确定真正起作用的是哪一步操作

9. vscode无法分析语法
  * 照ms官网文档装9.0
  * 装mono-complete
  * 让vscode选择wsl.sln进行加载
  * 在Preference中选择Rider而非VSCode执行项目文件生成（否则会因为windows绝对路径的Nuget引用导致语言服务器报错，没找到解决方式）
```
.vscode/settings.json
{
    "dotnet.preferCSharpExtension": true,
    "dotnet.defaultSolution": "DemoProj-wsl.sln",
    "omnisharp.defaultLaunchSolution": "DemoProj-wsl.sln"
    ...
}
```

10. 文件变动后无法立即看到最新linter
  * 因为wsl下无法及时监听文件变动，需要自写vs插件+mcp server让ai每次改完文件通知vscode进行刷新。但我写了一个似乎并没有起效，还在摸索中。

11. 工程内无法用wsl vscode打开文件
  * 让Unity打开这个bat，传参`"$(File)" $(Line)`
```
@echo off

set win_path=%1
set wsl_path=%win_path:\=/%

set wsl_path=%wsl_path:C:=/mnt/c%
set wsl_path=%wsl_path:D:=/mnt/d%
set wsl_path=%wsl_path:E:=/mnt/e%
set wsl_path=%wsl_path:F:=/mnt/f%
set wsl_path=%wsl_path:G:=/mnt/g%
set wsl_path=%wsl_path:H:=/mnt/h%
set wsl_path=%wsl_path:I:=/mnt/i%
set wsl_path=%wsl_path:J:=/mnt/j%

wsl.exe code -r --goto "%wsl_path%:%2" & disown & exit
```
12. 新增的代码文件，vscode提示不在当前workspace下
  * 原因未知，重新生成一遍项目文件就好了(Unity内可以直接调用Rider的接口：https://docs.unity3d.com/Packages/com.unity.ide.rider@1.2/api/Packages.Rider.Editor.ProjectGeneration.html) 可以考虑加进unity mcp能力中

#基本技巧
1. 在~/.claude/CLAUDE.md里写rules（你希望它如何思考），在~/.claude/settings.json里指定文本编辑器
```
{
    "env": {
        "EDITOR": "code"
    }
}
```
2. 在rules中强调，每次完成修改后必须执行git commit，以确保我们能撤销错误修改
3. 使用dll extractor mcp(https://github.com/Reekin/CSharpDllMetadataExtractor) 获取最准确的接口调用信息，避免大模型知识混乱

#使用体验

1. 虽然托管程度更高（一句话能完成更大量的任务且包含测试提交等完整过程），但编写质量并没有比RIPER5加持的cursor好多少。无论是在Unity技术栈的掌握程度，还是对游戏开发框架设计的经验理解，都不如它在web领域表现得那么好，时不时会写出一些实际项目中根本不可能采用的外行或初级的实现。
2. 基于1，根本不敢选择跳过检查，每一步修改都必须人工review。于是工作流程变成了输入指令-等待几十秒-review-调整的循环。人无法从中脱身，也就提升不了生产力，做不到一些人所说的让它自己跑一整晚。考虑到其编写水平，整体效率反而是下降的。而且很明显这种vibe coding的工作方式真的会让人变蠢。
3. 对IDE的调度程度不够：cc插件提供的IDE mcp能力似乎只有diff和diagnostic，人类在开发中高频使用的F12查看定义、shift+F12查看引用等功能，cc都做不了。很多时候只能看它为了找个class愚蠢地执行n次文件搜索指令。而且在跨系统的使用情境下，文件变动无法及时被感知，cc很多时候拿到的linter都是过期的，只能等他执行完任务你进Unity执行编译后它才能拿到正确的编译结果，对连贯性的影响非常严重。
4. 记忆/注意力问题：在现实开发过程中，我们提出需求并与程序沟通形成结论后，程序会按讨论结论开发并交付符合预期的结果。但在与cc的合作过程中，即使我在对话中明确给出了目标、技术选型、开发原则等信息，并让他记录在项目memory中，/clear后它重新从memory里读出来的理解与之前对话中差非常多，并给出偏差非常大的实现。目前看来项目下的CLAUDE.md还不足以支持“对话-形成文档-按文档开发”的流程，必须在每轮对话内用prompt强调才能有足够的引导效果。
5. GUI缺失，输入体验不佳，windows下不支持上传图片，各种体验细节问题。
