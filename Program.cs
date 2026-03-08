using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static readonly string[] TargetTypes =
    {
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.CardConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.RelicConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.GoldConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.PotionConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.UpgradeCardConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.RemoveCardConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.HealConsoleCmd",
        "MegaCrit.Sts2.Core.DevConsole.ConsoleCommands.EnergyConsoleCmd"
    };

    private const string RelativeDllPath = @"steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll";

    private static int Main(string[] args)
    {
        try
        {
            string? dllPath = null;

            if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
            {
                dllPath = args[0].Trim('"');
                Console.WriteLine($"입력 경로 사용: {dllPath}");
            }
            else
            {
                Console.WriteLine("입력 경로가 없어 Steam 라이브러리에서 sts2.dll 자동 탐색을 시도합니다...");
                dllPath = TryFindSts2Dll();
            }

            if (string.IsNullOrWhiteSpace(dllPath))
            {
                Console.WriteLine("sts2.dll을 찾지 못했습니다.");
                Console.WriteLine();
                Console.WriteLine("기본 예상 경로:");
                Console.WriteLine(@"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll");
                return 1;
            }

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"파일이 없습니다: {dllPath}");
                return 1;
            }

            Console.WriteLine($"대상 DLL: {dllPath}");

            string dllDir = Path.GetDirectoryName(dllPath)!;
            string backupPath = dllPath + ".bak";

            if (!File.Exists(backupPath))
            {
                File.Copy(dllPath, backupPath);
                Console.WriteLine($"백업 생성: {backupPath}");
            }
            else
            {
                Console.WriteLine($"백업 이미 존재: {backupPath}");
            }

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dllDir);

            var readerParams = new ReaderParameters
            {
                ReadWrite = false,
                InMemory = true,
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Immediate
            };

            using var assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParams);
            var module = assembly.MainModule;

            int okCount = 0;

            foreach (string fullTypeName in TargetTypes)
            {
                bool ok = EnsureDebugOnlyOverrideFalse(module, fullTypeName);
                Console.WriteLine($"- {GetShortName(fullTypeName)}: {(ok ? "OK" : "SKIP")}");
                if (ok)
                {
                    okCount++;
                }
            }

            if (okCount == 0)
            {
                Console.WriteLine("패치할 대상이 없어 중단합니다.");
                return 2;
            }

            string tempPath = dllPath + ".patched";

            var writerParams = new WriterParameters
            {
                WriteSymbols = false
            };

            assembly.Write(tempPath, writerParams);

            File.Copy(tempPath, dllPath, overwrite: true);
            File.Delete(tempPath);

            Console.WriteLine("패치 완료");
            Console.WriteLine($"성공: {okCount}/{TargetTypes.Length}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("패치 실패:");
            Console.WriteLine(ex);
            return 3;
        }
    }

    private static string? TryFindSts2Dll()
    {
        foreach (string libraryRoot in EnumerateSteamLibraryRoots())
        {
            try
            {
                string dllPath = Path.Combine(libraryRoot, RelativeDllPath);
                if (File.Exists(dllPath))
                {
                    Console.WriteLine($"발견: {dllPath}");
                    return dllPath;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraryRoots()
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] possibleSteamRoots =
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };

        foreach (string root in possibleSteamRoots)
        {
            if (Directory.Exists(root))
            {
                results.Add(root);
            }
        }

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                string root = drive.RootDirectory.FullName;
                string[] candidates =
                {
                    Path.Combine(root, "SteamLibrary"),
                    Path.Combine(root, "Games", "SteamLibrary"),
                    Path.Combine(root, "Program Files (x86)", "Steam"),
                    Path.Combine(root, "Program Files", "Steam")
                };

                foreach (string candidate in candidates)
                {
                    if (Directory.Exists(candidate))
                    {
                        results.Add(candidate);
                    }
                }
            }
            catch
            {
            }
        }

        var expanded = new HashSet<string>(results, StringComparer.OrdinalIgnoreCase);

        foreach (string steamRoot in results.ToArray())
        {
            foreach (string extra in ReadLibraryFolders(steamRoot))
            {
                expanded.Add(extra);
            }
        }

        return expanded;
    }

    private static IEnumerable<string> ReadLibraryFolders(string steamRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
                return result;

            foreach (string rawLine in File.ReadAllLines(vdfPath))
            {
                string line = rawLine.Trim();

                if (!line.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
                    continue;

                var quoted = ExtractQuotedValues(line).ToList();
                if (quoted.Count < 2)
                    continue;

                string pathValue = quoted[1].Replace(@"\\", @"\");
                if (Directory.Exists(pathValue))
                {
                    result.Add(pathValue);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static IEnumerable<string> ExtractQuotedValues(string line)
    {
        var values = new List<string>();
        int i = 0;

        while (i < line.Length)
        {
            int start = line.IndexOf('"', i);
            if (start < 0)
                break;

            int end = line.IndexOf('"', start + 1);
            if (end < 0)
                break;

            values.Add(line.Substring(start + 1, end - start - 1));
            i = end + 1;
        }

        return values;
    }

    private static bool EnsureDebugOnlyOverrideFalse(ModuleDefinition module, string fullTypeName)
    {
        var type = module.GetTypes().FirstOrDefault(t => t.FullName == fullTypeName);
        if (type == null)
        {
            Console.WriteLine($"타입을 찾지 못했습니다: {fullTypeName}");
            return false;
        }

        var baseType = type.BaseType?.Resolve();
        if (baseType == null)
        {
            Console.WriteLine($"베이스 타입을 resolve하지 못했습니다: {fullTypeName}");
            return false;
        }

        var baseGetter = baseType.Methods.FirstOrDefault(m =>
            m.Name == "get_DebugOnly" &&
            !m.HasParameters &&
            m.ReturnType.FullName == module.TypeSystem.Boolean.FullName);

        if (baseGetter == null)
        {
            Console.WriteLine($"부모 get_DebugOnly()를 찾지 못했습니다: {baseType.FullName}");
            return false;
        }

        var existing = type.Methods.FirstOrDefault(m =>
            m.Name == "get_DebugOnly" &&
            !m.HasParameters &&
            m.ReturnType.FullName == module.TypeSystem.Boolean.FullName);

        if (existing != null)
        {
            RewriteMethodToReturnFalse(existing);
            EnsurePropertyExists(type, module, existing);
            EnsureOverrideExists(existing, module, baseGetter);
            return true;
        }

        var getter = new MethodDefinition(
            "get_DebugOnly",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Virtual,
            module.TypeSystem.Boolean);

        getter.SemanticsAttributes = MethodSemanticsAttributes.Getter;
        getter.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;

        RewriteMethodToReturnFalse(getter);

        type.Methods.Add(getter);
        getter.Overrides.Add(module.ImportReference(baseGetter));

        EnsurePropertyExists(type, module, getter);

        return true;
    }

    private static void RewriteMethodToReturnFalse(MethodDefinition method)
    {
        method.Body = new MethodBody(method);
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 1;

        var il = method.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void EnsurePropertyExists(TypeDefinition type, ModuleDefinition module, MethodDefinition getter)
    {
        var prop = type.Properties.FirstOrDefault(p =>
            p.Name == "DebugOnly" &&
            p.PropertyType.FullName == module.TypeSystem.Boolean.FullName);

        if (prop != null)
        {
            prop.GetMethod = getter;
            return;
        }

        prop = new PropertyDefinition("DebugOnly", PropertyAttributes.None, module.TypeSystem.Boolean)
        {
            GetMethod = getter
        };

        type.Properties.Add(prop);
    }

    private static void EnsureOverrideExists(MethodDefinition method, ModuleDefinition module, MethodDefinition baseGetter)
    {
        bool alreadyExists = method.Overrides.Any(o =>
            o.FullName == module.ImportReference(baseGetter).FullName);

        if (!alreadyExists)
        {
            method.Overrides.Add(module.ImportReference(baseGetter));
        }
    }

    private static string GetShortName(string fullTypeName)
    {
        int idx = fullTypeName.LastIndexOf('.');
        return idx >= 0 ? fullTypeName[(idx + 1)..] : fullTypeName;
    }
}