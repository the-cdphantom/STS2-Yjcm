using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Yjcm.Loader;

[ModInitializer(nameof(Initialize))]
public static class LoaderBootstrap
{
    private const string ModId = "Yjcm";
    private const string VersionedDllPrefix = "Yjcm.";
    private static readonly object InitializationGate = new();
    private static Assembly? _realAssembly;
    private static bool _initializing;
    private static bool _initialized;

    public static void Initialize()
    {
        lock (InitializationGate)
        {
            if (_initialized || _initializing) return;
            _initializing = true;
            try { _initialized = InitializeCore(); }
            finally { _initializing = false; }
        }
    }

    private static bool InitializeCore()
    {
        string? modRoot = Path.GetDirectoryName(typeof(LoaderBootstrap).Assembly.Location);
        if (string.IsNullOrEmpty(modRoot))
        {
            Log.Error("[Yjcm.Loader] 无法定位 mod 根目录", 2);
            return false;
        }

        string hostText = ReleaseInfoManager.Instance?.ReleaseInfo?.Version ?? "";
        Version? host = TryParseVersion(hostText);
        string? realDll = PickRealDll(modRoot, host);
        if (realDll == null)
        {
            Log.Error($"[Yjcm.Loader] 未找到可用真实 DLL（宿主版本 {hostText}）", 2);
            return false;
        }

        Assembly real;
        try
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(LoaderBootstrap).Assembly)
                          ?? AssemblyLoadContext.Default;
            real = context.LoadFromAssemblyPath(realDll);
        }
        catch (Exception ex)
        {
            Log.Error($"[Yjcm.Loader] 加载真实 DLL 失败: {ex}", 2);
            return false;
        }

        _realAssembly = real;
        AssociateAssemblyWithMod(real);
        if (!InvokeRequiredBootstrap(real, "Yjcm.MainFile", "Initialize", "内容初始化"))
        {
            _realAssembly = null;
            return false;
        }

        try { ModManager.OnModDetected += OnModDetected; }
        catch (Exception ex) { Log.Warn($"[Yjcm.Loader] 订阅 OnModDetected 失败: {ex.Message}", 2); }
        Log.Info($"[Yjcm.Loader] 已加载变体 {Path.GetFileName(realDll)}（宿主版本 {hostText}）", 2);
        return true;
    }

    private static bool InvokeRequiredBootstrap(Assembly real, string typeName, string methodName, string label)
    {
        try
        {
            MethodInfo? method = real.GetType(typeName, false)?.GetMethod(
                methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (method == null)
            {
                Log.Error($"[Yjcm.Loader] 未找到必需的{label}入口 {typeName}.{methodName}", 2);
                return false;
            }
            method.Invoke(null, null);
            Log.Info($"[Yjcm.Loader] {label}入口调用完成", 2);
            return true;
        }
        catch (TargetInvocationException ex)
        {
            Log.Error($"[Yjcm.Loader] {label}入口调用失败: {ex.InnerException ?? ex}", 2);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"[Yjcm.Loader] {label}入口调用失败: {ex}", 2);
            return false;
        }
    }

    private static string? PickRealDll(string modRoot, Version? host)
    {
        var candidates = Directory.GetFiles(modRoot, "Yjcm.*.dll")
            .Select(path => (Path: path, Version: TryParseFileVersion(Path.GetFileName(path))))
            .Where(item => item.Version != null)
            .OrderBy(item => item.Version)
            .ToList();
        if (candidates.Count == 0) return null;
        if (host == null) return candidates[^1].Path;
        return candidates.LastOrDefault(item => item.Version! <= host).Path ?? candidates[^1].Path;
    }

    private static Version? TryParseFileVersion(string fileName)
    {
        if (!fileName.StartsWith(VersionedDllPrefix, StringComparison.Ordinal)
            || !fileName.EndsWith(".dll", StringComparison.Ordinal)) return null;
        string text = fileName[VersionedDllPrefix.Length..^4];
        return Version.TryParse(text, out Version? version) ? version : null;
    }

    private static Version? TryParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        int suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0) text = text[..suffix];
        if (text.Length >= 2 && (text[0] is 'v' or 'V') && char.IsDigit(text[1])) text = text[1..];
        return Version.TryParse(text, out Version? version) ? version : null;
    }

    private static void AssociateAssemblyWithMod(Assembly real)
    {
        try
        {
            MethodInfo? method = typeof(ModManager).GetMethod(
                "AssociateAssemblyWithMod", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(null, [ModId, real]);
                Log.Info("[Yjcm.Loader] 已通过 AssociateAssemblyWithMod 关联真实 DLL", 2);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[Yjcm.Loader] AssociateAssemblyWithMod 调用失败，改用回退路径: {ex.Message}", 2);
        }

        try
        {
            FieldInfo? modsField = typeof(ModManager).GetField("_mods", BindingFlags.Static | BindingFlags.NonPublic);
            if (modsField?.GetValue(null) is not IList mods) return;
            foreach (object mod in mods)
            {
                object? manifest = mod.GetType().GetField(
                    "manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(mod);
                string? id = manifest?.GetType().GetProperty(
                    "id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(manifest) as string
                    ?? manifest?.GetType().GetField(
                        "id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(manifest) as string;
                if (id != ModId) continue;
                AttachAssembly(mod, real);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[Yjcm.Loader] 关联真实 DLL 失败: {ex}", 2);
        }
    }

    private static void AttachAssembly(object mod, Assembly real)
    {
        Type type = mod.GetType();
        if (type.GetField("assemblies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(mod) is IList assemblies)
        {
            if (!assemblies.Contains(real)) assemblies.Add(real);
            return;
        }
        type.GetField("assembly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(mod, real);
    }

    private static void OnModDetected(Mod mod)
    {
        if (mod.manifest?.id != ModId || _realAssembly == null) return;
        try { AttachAssembly(mod, _realAssembly); }
        catch (Exception ex) { Log.Error($"[Yjcm.Loader] OnModDetected 重设失败: {ex.Message}", 2); }
    }
}
