﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Fabl;

namespace Entitas.CodeGenerator.CLI {

    class MainClass {

        static Logger _logger = fabl.GetLogger("Main");

        public static void Main(string[] args) {
            if(args == null || args.Length == 0) {
                printUsage();
                return;
            }

            setupLogging(args);

            try {
                switch(args[0]) {
                    case "new":
                        createNewConfig(args.Any(arg => arg == "-f"));
                        break;
                    case "edit":
                        editConfig();
                        break;
                    case "doctor":
                        doctor();
                        break;
                    case "diff":
                        diff();
                        break;
                    case "scan":
                        scanDlls();
                        break;
                    case "dry":
                        dryRun();
                        break;
                    case "gen":
                        generate();
                        break;
                    default:
                        printUsage();
                        break;
                }
            } catch(Exception ex) {
                var loadException = ex as ReflectionTypeLoadException;
                if(loadException != null) {
                    foreach(var e in loadException.LoaderExceptions) {
                        _logger.Error(e.ToString());
                    }
                } else {
                    _logger.Error(ex.Message);
                }
            }
        }

        static void printUsage() {
            Console.WriteLine(
@"usage: entitas new [-f]     - Creates new Entitas.properties with default values
       entitas edit         - Opens Entitas.properties
       entitas doctor       - Checks the config for potential problems
       entitas diff         - List of unused and invalid data providers, code generators and post processors
       entitas scan         - Scans and prints available types found in specified assemblies
       entitas dry          - Simulates generating files without running post processors
       entitas gen          - Generates files based on Entitas.properties
       [-v]                 - verbose output"
            );
        }

        static void setupLogging(string[] args) {
            if(args.Any(arg => arg == "-v")) {
                fabl.globalLogLevel = LogLevel.On;
            } else {
                fabl.globalLogLevel = LogLevel.Info;
            }

            var formatter = new ColorCodeFormatter();
            fabl.AddAppender((logger, logLevel, message) => {
                Console.WriteLine(formatter.FormatMessage(logLevel, message));
            });
        }

        static void createNewConfig(bool force) {
            var currentDir = Directory.GetCurrentDirectory();
            var path = currentDir + Path.DirectorySeparatorChar + EntitasPreferences.GetConfigPath();

            if(!File.Exists(path) || force) {
                var types = AppDomain.CurrentDomain.GetAllTypes();
                var defaultConfig = new CodeGeneratorConfig(
                    new EntitasPreferencesConfig(string.Empty),
                    CodeGeneratorUtil.GetOrderedTypeNames<ICodeGeneratorDataProvider>(types).ToArray(),
                    CodeGeneratorUtil.GetOrderedTypeNames<ICodeGenerator>(types).ToArray(),
                    CodeGeneratorUtil.GetOrderedTypeNames<ICodeGenFilePostProcessor>(types).ToArray()
                );

                var config = defaultConfig.ToString();
                File.WriteAllText(path, config);
                _logger.Info("Created " + path);
                _logger.Debug(config);

                editConfig();
            } else {
                _logger.Warn(path + " already exists!");
                _logger.Info("Use entitas new -f to overwrite exiting file.");
            }
        }

        static void editConfig() {
            _logger.Debug("Opening " + EntitasPreferences.GetConfigPath());
            System.Diagnostics.Process.Start(EntitasPreferences.GetConfigPath());
        }

        static void doctor() {
            if(File.Exists(EntitasPreferences.GetConfigPath())) {
                diff();
                _logger.Debug("Dry Run");
                CodeGeneratorUtil
                    .CodeGeneratorFromConfig(EntitasPreferences.GetConfigPath())
                    .DryRun();
            } else {
                printNoConfig();
            }
        }

        static void diff() {
            if(File.Exists(EntitasPreferences.GetConfigPath())) {
                var fileContent = File.ReadAllText(EntitasPreferences.GetConfigPath());
                var properties = new Properties(fileContent);
                var config = new CodeGeneratorConfig(new EntitasPreferencesConfig(fileContent));
                var types = CodeGeneratorUtil.LoadTypesFromCodeGeneratorAssemblies();

                printUnusedKeys(properties);
                printMissingKeys(properties);

                printUnavailable(CodeGeneratorUtil.GetUnavailable<ICodeGeneratorDataProvider>(types, config.dataProviders));
                printUnavailable(CodeGeneratorUtil.GetUnavailable<ICodeGenerator>(types, config.codeGenerators));
                printUnavailable(CodeGeneratorUtil.GetUnavailable<ICodeGenFilePostProcessor>(types, config.postProcessors));

                printAvailable(CodeGeneratorUtil.GetAvailable<ICodeGeneratorDataProvider>(types, config.dataProviders));
                printAvailable(CodeGeneratorUtil.GetAvailable<ICodeGenerator>(types, config.codeGenerators));
                printAvailable(CodeGeneratorUtil.GetAvailable<ICodeGenFilePostProcessor>(types, config.postProcessors));
            } else {
                printNoConfig();
            }
        }

        static void printUnusedKeys(Properties properties) {
            var unusedKeys = properties.keys.Where(key => !CodeGeneratorConfig.keys.Contains(key));
            foreach(var key in unusedKeys) {
                _logger.Info("Unused key: " + key);
            }
        }

        static void printMissingKeys(Properties properties) {
            var missingKeys = CodeGeneratorConfig.keys.Where(key => !properties.HasKey(key));
            foreach(var key in missingKeys) {
                _logger.Warn("Missing key: " + key);
            }
        }

        static void scanDlls() {
            if(File.Exists(EntitasPreferences.GetConfigPath())) {
                printTypes(CodeGeneratorUtil.LoadTypesFromCodeGeneratorAssemblies());
                printTypes(CodeGeneratorUtil.LoadTypesFromAssemblies());
            } else {
                printNoConfig();
            }
        }

        static void printTypes(Type[] types) {
            var orderedTypes = types
                .OrderBy(type => type.Assembly.GetName().Name)
                .ThenBy(type => type.FullName);
            foreach(var type in orderedTypes) {
                _logger.Info(type.Assembly.GetName().Name + ": " + type);
            }
        }

        static void dryRun() {
            if(File.Exists(EntitasPreferences.GetConfigPath())) {
                CodeGeneratorUtil
                    .CodeGeneratorFromConfig(EntitasPreferences.GetConfigPath())
                    .DryRun();
            } else {
                printNoConfig();
            }
        }

        static void generate() {
            if(File.Exists(EntitasPreferences.GetConfigPath())) {
                CodeGeneratorUtil
                    .CodeGeneratorFromConfig(EntitasPreferences.GetConfigPath())
                    .Generate();
            } else {
                printNoConfig();
            }
        }

        static void printUnavailable(string[] names) {
            foreach(var name in names) {
                _logger.Warn("Unavailable: " + name);
            }
        }

        static void printAvailable(string[] names) {
            foreach(var name in names) {
                _logger.Info("Available: " + name);
            }
        }

        static void printNoConfig() {
            _logger.Info("Couldn't find " + EntitasPreferences.GetConfigPath());
            _logger.Info("Run 'entitas new' to create Entitas.properties with default values");
        }
    }
}
