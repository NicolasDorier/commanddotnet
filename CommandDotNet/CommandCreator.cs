﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommandDotNet.Attributes;
using CommandDotNet.Exceptions;
using CommandDotNet.MicrosoftCommandLineUtils;
using CommandDotNet.Models;

namespace CommandDotNet
{
    public class CommandCreator
    {
        private readonly Type _type;
        private readonly CommandLineApplication _app;
        private readonly AppSettings _settings;
        private readonly CommandRunner _commandRunner;

        public CommandCreator(Type type, CommandLineApplication app, IDependencyResolver dependencyResolver, AppSettings settings)
        {
            _type = type;
            _app = app;
            _settings = settings;
            
            //get values for construtor params
            IEnumerable<ArgumentInfo> constructorValues = GetOptionValuesForConstructor();
            
            _commandRunner = new CommandRunner(app, type, constructorValues, dependencyResolver);
        }

        public void CreateDefaultCommand()
        {
            CommandInfo defaultCommandInfo = _type.GetDefaultCommandInfo(_settings);
            
            _app.OnExecute(async () =>
            {
                if (defaultCommandInfo != null)
                {
                    if (defaultCommandInfo.Arguments.Any())
                    {
                        throw new AppRunnerException("Method with [DefaultMethod] attribute does not support parameters");
                    }

                    return await _commandRunner.RunCommand(defaultCommandInfo, null);
                }

                _app.ShowHelp();
                return 0;
            });
        }

        public void CreateCommands()
        {            
            foreach (CommandInfo commandInfo in _type.GetCommandInfos(_settings))
            {
                List<ArgumentInfo> argumentValues = new List<ArgumentInfo>();

                CommandLineApplication commandOption = _app.Command(commandInfo.Name, command =>
                {
                    command.Description = commandInfo.Description;

                    command.ExtendedHelpText = commandInfo.ExtendedHelpText;

                    command.AllowArgumentSeparator = _settings.AllowArgumentSeparator;

                    command.Syntax = commandInfo.Syntax;
                    
                    command.HelpOption(Constants.HelpTemplate);
                      
                    foreach (ArgumentInfo argument in commandInfo.Arguments)
                    {
                        argumentValues.Add(argument);
                        switch (argument)
                        {
                            case CommandOptionInfo option:
                                SetValueForOption(option, command);
                                break;
                            case CommandParameterInfo parameter:
                                SetValueForParameter(parameter, command);
                                break;
                        }
                    }
                }, throwOnUnexpectedArg: _settings.ThrowOnUnexpectedArgument);

                commandOption.OnExecute(async () => await _commandRunner.RunCommand(commandInfo, argumentValues));
            }
        }

        private static void SetValueForParameter(CommandParameterInfo parameter, CommandLineApplication command)
        {
            parameter.SetValue(command.Argument(
                parameter.Name,
                parameter.EffectiveDescription,
                parameter.IsMultipleType));
        }

        private static void SetValueForOption(CommandOptionInfo option, CommandLineApplication command)
        {
            option.SetValue(command.Option(option.Template,
                option.EffectiveDescription,
                option.CommandOptionType,
                option.Inherited));
        }

        private IEnumerable<ArgumentInfo> GetOptionValuesForConstructor()
        {
            IEnumerable<ParameterInfo> parameterInfos = _type
                .GetConstructors()
                .FirstOrDefault()
                .GetParameters();

            if(parameterInfos.Any(p => p.HasAttribute<ArgumentAttribute>()))
                throw new AppRunnerException("Constructor arguments can not have [Argument] attribute. Please use [Option] attribute");

            ArgumentInfoCreator argumentInfoCreator = new ArgumentInfoCreator(_settings);
            
            List<ArgumentInfo> argumentInfos = new List<ArgumentInfo>();

            foreach (var parameterInfo in parameterInfos)
            {
                argumentInfos.AddRange(argumentInfoCreator.ConvertToArgumentInfos(parameterInfo, ArgumentMode.Option));
            }

            foreach (ArgumentInfo argumentInfo in argumentInfos)
            {
                var optionInfo = (CommandOptionInfo) argumentInfo;
                optionInfo.SetValue(_app.Option(
                    optionInfo.Template,
                    optionInfo.EffectiveDescription,
                    optionInfo.CommandOptionType,
                    optionInfo.Inherited));
            }
            
            return argumentInfos;
        }
    }
}