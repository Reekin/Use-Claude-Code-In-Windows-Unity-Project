using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using Packages.Rider.Editor.ProjectGeneration;
using UnityMcpBridge.Editor.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace GameFramework.Editor
{
    /// <summary>
    /// Unity MCP工具：项目文件重新生成器
    /// 在WSL环境下实现可靠的后台编译支持
    /// </summary>
    public class ProjectFilesRegeneratorTool : IUnityMcpTool
    {
        private static readonly string CompileStatusFile = Path.Combine(Application.temporaryCachePath, "mcp_compile_status.json");
        private static bool _originalRunInBackground = false;
        private static bool _backgroundStateChanged = false;
        
        static ProjectFilesRegeneratorTool()
        {
            // 注册编译事件监听
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            
            // 注册焦点变化监听
            EditorApplication.focusChanged += OnEditorFocusChanged;
        }
        
        private static void OnEditorFocusChanged(bool hasFocus)
        {
            // 如果失去焦点且有编译任务，确保后台运行启用
            if (!hasFocus && IsCompiling())
            {
                if (!Application.runInBackground)
                {
                    Application.runInBackground = true;
                    _backgroundStateChanged = true;
                }
            }
        }
        
        private static bool IsCompiling()
        {
            if (!File.Exists(CompileStatusFile)) return false;
            
            try
            {
                string json = File.ReadAllText(CompileStatusFile);
                var status = JsonConvert.DeserializeAnonymousType(json, new { isCompiling = false });
                return status.isCompiling;
            }
            catch
            {
                return false;
            }
        }

        private static void OnCompilationStarted(object obj)
        {
            Debug.Log("[ProjectFilesRegeneratorTool] 编译开始");
            UpdateCompileStatus(true, "编译进行中");
        }

        private static void OnCompilationFinished(object obj)
        {
            Debug.Log("[ProjectFilesRegeneratorTool] 编译完成");
            UpdateCompileStatus(false, "编译完成");
            RestoreBackgroundState();
        }
        
        private static void UpdateCompileStatus(bool isCompiling, string message)
        {
            if (!File.Exists(CompileStatusFile) && !isCompiling) return;
            
            try
            {
                var status = new
                {
                    isCompiling,
                    message,
                    lastUpdate = DateTime.Now,
                    compileId = $"compile_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                
                File.WriteAllText(CompileStatusFile, JsonConvert.SerializeObject(status, Formatting.Indented));
            }
            catch
            {
                // 忽略状态文件写入错误
            }
        }
        
        private static void RestoreBackgroundState()
        {
            if (_backgroundStateChanged)
            {
                Application.runInBackground = _originalRunInBackground;
                _backgroundStateChanged = false;
            }
        }
        
        public string CommandType => "project_files_refresher";
        public string Description => "(每次执行完代码修改后必须调用)刷新Unity项目文件并重新编译";

        public McpToolMetadata GetToolMetadata()
        {
            return new McpToolMetadata
            {
                CommandType = "project_files_refresher",
                Description = "刷新Unity项目文件并重新编译",
                ReturnDescription = "执行结果，包含成功状态和消息",
                Parameters = new List<McpToolParameter>()
            };
        }

        public object HandleCommand(JObject parameters)
        {
            try
            {
                return StartRegenerateAndCompile();
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = $"执行命令时发生错误: {ex.Message}",
                    error = ex.Message
                };
            }
        }

        private object StartRegenerateAndCompile()
        {
            Debug.Log("[ProjectFilesRegeneratorTool] 编译启动");
            
            try
            {
                // 启用后台运行以确保编译能继续
                _originalRunInBackground = Application.runInBackground;
                if (!Application.runInBackground)
                {
                    Application.runInBackground = true;
                    _backgroundStateChanged = true;
                }
                
                // 设置编译状态
                UpdateCompileStatus(true, "编译开始");
                
                // 执行核心操作
                var projectGeneration = new ProjectGeneration();
                projectGeneration.Sync();
                
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                
                EditorApplication.QueuePlayerLoopUpdate();
                CompilationPipeline.RequestScriptCompilation();
                
                return new
                {
                    success = true,
                    message = "项目文件重新生成和编译已启动",
                    background_enabled = Application.runInBackground
                };
            }
            catch (Exception e)
            {
                UpdateCompileStatus(false, $"启动失败: {e.Message}");
                RestoreBackgroundState();
                
                return new
                {
                    success = false,
                    message = $"启动重新生成项目文件或重新编译时发生错误: {e.Message}",
                    error = e.Message
                };
            }
        }
    }

    // Unity编辑器菜单项
    public class ProjectFilesRegenerator
    {
        [MenuItem("Tools/Regenerate Project Files and Recompile")]
        public static void RegenerateAndRecompile()
        {
            var tool = new ProjectFilesRegeneratorTool();
            tool.HandleCommand(new JObject());
        }
    }
}