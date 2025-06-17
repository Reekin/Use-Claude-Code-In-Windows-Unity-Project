# How To Use Claude Code In Windows Unity Project
[English](./README.md)

记录尝试在Unity项目中使用Claude Code踩过的各种坑

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
  * 在wsl中安装.Net 8.0 SDK，然后在settings - Build, Execution, Deployment -Toolset and Build 中手动将.NET CLI指定为/usr/lib/dotnet/，MSBuild会自动匹配
  * 在wsl中安装mono-complete
    * 装完会导致wsl下无法正常执行exe，需要将文末代码1加入wsl的~/.profile内，每次启动wsl时会自动执行一次修正设置

5. Rider remote wsl加载Unity项目后无法识别任何符号
  * wsl下的sln和csproj中的文件路径还是windows格式，需要替换为/mnt/c/开头的wsl格式
  * 将文末代码2添加至Assets/Editor/目录下，再进Preferences-External Tools重新生成一次项目文件，现在会每次多生成一批wsl专供的项目文件

6. Rider remote wsl terminal进入claude后无法Esc返回上一级
  * settings-keymap-搜索escape找到Plugins/Terminal/Escape，清除快捷键


Code1:
```
# https://github.com/microsoft/WSL/issues/5466
python.exe --version >/dev/null 2>&1 || \
sudo update-binfmts --disable cli
```

Code2:
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
