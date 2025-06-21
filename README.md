[阅读中文版](./README-CN.md)

# Using Claude Code for Unity Development on Windows - ~~Guide~~ Deterrent

I spent several nights trying to get Claude Code (CC) working with Unity projects on Windows. Here's a record of the various issues encountered and their solutions.

**TL;DR upfront**: At this stage, I don't recommend attempting this workflow - CC doesn't natively support Windows, it's one pitfall after another, and the cost of solving various engineering configuration issues is quite high. Even after getting it working, the actual experience isn't as amazing as expected.

The basic installation steps are: install WSL first, then install CC in WSL (https://docs.anthropic.com/en/docs/claude-code/setup), then open your IDE on Windows with remote connection to WSL and open the Unity project from there. I won't go into too much detail about the regular setup and usage steps - check the documentation yourself and ask AI when you encounter problems.

## **Issues and Solutions**

### 1. `/ide` command reports IDE not found after starting Claude in Cursor WSL terminal
- Need to first use `find` in WSL to locate the claude-code.vsix file, then install it in PowerShell
- Remote WSL to open the Windows project directory

### 2. MCP used by Cursor cannot be loaded by Claude Code
- When using remote WSL, the IDE only recognizes WSL-format paths, so you need to replace paths in the mcp.json prepared for Claude to start with `/mnt/c/`
- Python scripts cannot share the same venv between WSL and Windows - need to copy a separate copy for each
- EXE programs should be directly callable

### 3. Cursor cannot properly perform C# syntax analysis on the project
- Microsoft officially restricts C# series plugins from being provided in non-official VSCode versions. Although Cursor provides its own C# plugin, the VSCode Unity plugin still depends on the official version
- You can try Cursor C# + dotRush plugin, but I haven't tested it because I switched to Rider

### 4. Rider remote WSL cannot properly load Unity project, reports missing MSBuild
- Install .NET 9.0 SDK in WSL according to Microsoft's official website, then manually specify .NET CLI as `/usr/lib/dotnet/` in Settings → Build, Execution, Deployment → Toolset and Build, MSBuild will automatically match
- Install mono-complete in WSL
  - After installation, WSL cannot properly execute exe files. Need to add this code to WSL's `~/.profile`, which will automatically execute once to fix settings every time WSL starts
```
# https://github.com/microsoft/WSL/issues/5466
python.exe --version >/dev/null 2>&1 || \
sudo update-binfmts --disable cli
```

### 5. Rider remote WSL loads Unity project but cannot recognize any symbols
- File paths in sln and csproj under WSL are still in Windows format, need to replace with WSL format starting with `/mnt/c/`
- Add this code 2 (at the end) to the `Assets/Editor/` directory, then go to Preferences → External Tools to regenerate project files once. Now it will generate an additional batch of WSL-specific project files each time
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

### 6. Cannot use Esc to return to previous level in Rider remote WSL terminal after entering Claude
- Settings → Keymap → search for "escape" and find "Plugins/Terminal/Escape", clear the shortcut

### 7. Calling `ide diagnostic` in Rider fails, always timeout
- This problem suddenly appeared during normal use, never found the cause, finally switched to VSCode

### 8. Cannot communicate properly with Unity MCP server
- Requests from WSL are blocked by Windows firewall. Need to find WSL's IP (`ip addr show eth0`), then create a new inbound rule in "Windows Defender Firewall with Advanced Security" → Custom, allowing all access from this IP
- But sometimes it doesn't take effect immediately. I also tried operations like `netsh advfirewall reset` to reset the firewall, not sure which operation actually worked

### 9. VSCode cannot analyze syntax
- Install 9.0 according to Microsoft's official documentation
- Install mono-complete
- Let VSCode select wsl.sln for loading
- Choose Rider instead of VSCode in Preferences for project file generation (otherwise the language server will error due to Windows absolute path NuGet references, no solution found)

```json
.vscode/settings.json
{
    "dotnet.preferCSharpExtension": true,
    "dotnet.defaultSolution": "DemoProj-wsl.sln",
    "omnisharp.defaultLaunchSolution": "DemoProj-wsl.sln"
    ...
}
```

### 10. Cannot immediately see latest linter after file changes
- Since WSL cannot monitor file changes in time, need to write a custom VS plugin + MCP server to let AI notify VSCode to refresh after each file modification. But the one I wrote doesn't seem to work, still exploring.

### 11. Cannot open files with WSL VSCode within the project
- Let Unity open this bat file, passing parameters `"$(File)" $(Line)`

```batch
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

### 12. Newly added code files, VSCode prompts they are not in the current workspace
- Reason unknown, regenerating project files once fixes it (Unity can directly call Rider's interface: https://docs.unity3d.com/Packages/com.unity.ide.rider@1.2/api/Packages.Rider.Editor.ProjectGeneration.html), could consider adding this to Unity MCP capabilities

## Basic Tips

1. Write rules in `~/.claude/CLAUDE.md` (how you want it to think), specify text editor in `~/.claude/settings.json`
```json
{
    "env": {
        "EDITOR": "code"
    }
}
```

2. Emphasize in rules that git commit must be executed after each modification to ensure we can undo incorrect changes

3. Use dll extractor MCP (https://github.com/Reekin/CSharpDllMetadataExtractor) to get the most accurate interface calling information, avoiding confusion from large model knowledge

## **User Experience**

1. Although the level of automation is higher (one sentence can complete larger tasks including testing, commits, and other complete processes), the writing quality is not much better than Cursor with RIPER5. Whether in Unity technology stack mastery or experience in game development framework design, it doesn't perform as well as in web development, occasionally writing implementations that would never be adopted in actual projects - either amateur or junior level.

2. Based on point 1, you can't dare to skip checks - every modification must be manually reviewed. So the workflow becomes: input command → wait dozens of seconds → review → adjust. Humans cannot break away from this cycle, so productivity doesn't improve, and you can't achieve what some people say about letting it run all night. Considering its writing level, overall efficiency actually decreases. And obviously this vibe coding work style really makes people dumber.

3. Insufficient IDE control: The IDE MCP capabilities provided by the CC plugin seem to only include diff and diagnostic. High-frequency functions used by humans in development like F12 to view definitions, Shift+F12 to view references, etc., CC cannot do. Often you can only watch it stupidly execute n file search commands to find a class. Moreover, in cross-system usage scenarios, file changes cannot be perceived in time, and CC often gets outdated linter information. You can only wait for it to complete tasks, then go into Unity to compile before it can get correct compilation results, which seriously affects continuity.

4. Memory/attention issues: In real development processes, after we propose requirements and communicate with programmers to form conclusions, programmers will develop according to the discussion conclusions and deliver results that meet expectations. But in the collaboration process with CC, even if I clearly provide goals, technology choices, development principles and other information in the conversation, and have it recorded in project memory, after `/clear` when it re-reads from memory, the understanding differs greatly from the previous conversation, giving implementations with very large deviations. Currently, it seems that CLAUDE.md under the project is not sufficient to support the "dialogue → form documentation → develop according to documentation" process. You must emphasize with prompts in each round of dialogue to have sufficient guiding effect.

5. GUI missing, poor input experience, Windows doesn't support image uploads, various experience detail issues.

## Code Snippets

### Code 1 (for ~/.profile)
```bash
# Fix mono/exe execution issues in WSL
export PATH="/usr/bin:$PATH"
```

### Code 2 (for Assets/Editor/)
```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

public class WSLProjectFileGenerator : AssetPostprocessor
{
    static void OnGeneratedCSProjectFiles()
    {
        string[] projectFiles = Directory.GetFiles(".", "*.csproj");
        foreach (string projectFile in projectFiles)
        {
            string content = File.ReadAllText(projectFile);
            string wslContent = content.Replace(@"C:\", "/mnt/c/")
                                     .Replace(@"D:\", "/mnt/d/")
                                     .Replace(@"E:\", "/mnt/e/")
                                     .Replace(@"F:\", "/mnt/f/")
                                     .Replace(@"G:\", "/mnt/g/")
                                     .Replace(@"H:\", "/mnt/h/")
                                     .Replace(@"I:\", "/mnt/i/")
                                     .Replace(@"J:\", "/mnt/j/")
                                     .Replace("\\", "/");
            
            string wslProjectFile = projectFile.Replace(".csproj", "-wsl.csproj");
            File.WriteAllText(wslProjectFile, wslContent);
        }
        
        string slnFile = Directory.GetFiles(".", "*.sln")[0];
        if (File.Exists(slnFile))
        {
            string slnContent = File.ReadAllText(slnFile);
            string wslSlnContent = slnContent.Replace(@"C:\", "/mnt/c/")
                                            .Replace(@"D:\", "/mnt/d/")
                                            .Replace(@"E:\", "/mnt/e/")
                                            .Replace(@"F:\", "/mnt/f/")
                                            .Replace(@"G:\", "/mnt/g/")
                                            .Replace(@"H:\", "/mnt/h/")
                                            .Replace(@"I:\", "/mnt/i/")
                                            .Replace(@"J:\", "/mnt/j/")
                                            .Replace("\\", "/")
                                            .Replace(".csproj", "-wsl.csproj");
            
            string wslSlnFile = slnFile.Replace(".sln", "-wsl.sln");
            File.WriteAllText(wslSlnFile, wslSlnContent);
        }
    }
}
```
