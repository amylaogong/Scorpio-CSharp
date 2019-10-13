﻿using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Scorpio.Tools;
using System.Linq;
using System;

namespace Scorpio.ScorpioReflect {
    public partial class GenerateScorpioClass {
        private static Dictionary<string, string> Operators = new Dictionary<string, string>() {
            {"op_Addition", "+"},
            {"op_Subtraction", "-"},
            {"op_Multiply", "*"},
            {"op_Division", "/"},
            {"op_Modulus", "%"},
            {"op_BitwiseOr", "|"},
            {"op_BitwiseAnd", "&"},
            {"op_ExclusiveOr", "^"},
            {"op_GreaterThan", ">"},
            {"op_LessThan", "<"},
            {"op_Equality", "=="},
            {"op_Inequality", "!="},
        };
        //获得构造函数
        private string GenerateConstructor() {
            var Constructors = m_Type.GetConstructors(ScorpioReflectUtil.BindingFlag);
            var builder = new StringBuilder();
            for (var i = 0; i < Constructors.Length; ++i) {
                var con = Constructors[i];
                var pars = con.GetParameters();
                var call = $"new __fullname({GetScorpioMethodCall(pars)})";
                builder.AppendFormat(@"
                case {0}: {{ {1} }}", i, GetExecuteMethod(pars, true, call));
            }
            string str = MethodTemplate;
            str = str.Replace("__getallmethod", GetAllMethod(Constructors));
            str = str.Replace("__name", m_ScorpioClassName + "_Constructor");
            str = str.Replace("__methodname", "Constructor");
            str = str.Replace("__execute", builder.ToString());
            return str;
        }
        private string GenerateMethod() {
            StringBuilder builder = new StringBuilder();
            foreach (var name in m_MethodNames) {
                builder.Append(GenerateMethodExecute(name));
            }
            return builder.ToString();
        }
        //运算符重载函数 + - * / [] 等
        private string GetSpeciaMethodExecute(MethodInfo method, string variable, ParameterInfo[] pars) {
            if (!method.IsSpecialName) { return ""; }
            string name = method.Name;
            if (Operators.ContainsKey(name)) {
                return string.Format("return {0} {1} {2};", GetScorpioMethodArgs(pars, 0), Operators[name], GetScorpioMethodArgs(pars, 1));
            }
            //重载 =
            if (name == "op_Implicit") {
                return string.Format("return ({0})({1});", ScorpioReflectUtil.GetFullName(method.ReturnType), GetScorpioMethodArgs(pars, 0));
            //如果 get_Item 参数是一个 就是 [] 的重载
            } else if (name == "get_Item" && method.GetParameters().Length == 1) {
                return string.Format("return {0}[{1}];", variable, GetScorpioMethodArgs(pars, 0));
            //如果 set_Item 参数是两个 就是 [] 的重载
            } else if (name == "set_Item" && method.GetParameters().Length == 2) {
                return string.Format("{0}[{1}] = {2}; return null;", variable, GetScorpioMethodArgs(pars, 0), GetScorpioMethodArgs(pars, 1));
            }
            return "";
        }
        private string GetEventMethodExecute(MethodInfo method, string variable, ParameterInfo[] pars) {
            foreach (var eve in m_AllEvents) {
                if (eve.GetAddMethod() == method) {
                    return string.Format("{0}.{1} += {2}; return null;", variable, eve.Name, GetScorpioMethodArgs(pars, 0));
                } else if (eve.GetRemoveMethod() == method) {
                    return string.Format("{0}.{1} -= {2}; return null;", variable, eve.Name, GetScorpioMethodArgs(pars, 0));
                }
            }
            return "";
        }
        private string GetPropertyMethodExecute(MethodInfo method, string variable, ParameterInfo[] pars) {
            foreach (var property in m_AllPropertys) {
                if (property.GetGetMethod() == method) {
                    return string.Format("return {0}.{1};", variable, property.Name);
                } else if (property.GetSetMethod() == method) {
                    return string.Format("{0}.{1} = {2}; return null;", variable, property.Name, GetScorpioMethodArgs(pars, 0));
                }
            }
            return "";
        }
        //生成一个函数的类
        private string GenerateMethodExecute(string name) {
            var builder = new StringBuilder();
            var methods = m_Methods.Where(_ => _.Name == name).ToArray();
            for (var i = 0; i < methods.Length; ++i) {
                var method = methods[i];
                var variable = method.IsStatic ? m_FullName : $"(({m_FullName})obj)";
                var pars = method.GetParameters();
                //运算符重载函数 + - * / [] 等
                string execute = GetSpeciaMethodExecute(method, variable, pars);
                if (!string.IsNullOrEmpty(execute)) { goto finish; }
                //是否是event函数
                execute = GetEventMethodExecute(method, variable, pars);
                if (!string.IsNullOrEmpty(execute)) { goto finish; }
                //是否是 get set 函数
                execute = GetPropertyMethodExecute(method, variable, pars);
                if (!string.IsNullOrEmpty(execute)) { goto finish; }
                var call = $"{variable}.{name}({GetScorpioMethodCall(pars)})";
                execute = GetExecuteMethod(pars, method.ReturnType != typeof(void), call);
            finish:
                builder.AppendFormat(@"
                case {0}: {{ {1} }}", i, execute);
            }
            var allMethodBuilder = new StringBuilder(GetAllMethod(methods.ToArray()));
            var extensionMethods = m_ExtensionMethods.Where(_ => _.Name == name).ToArray();
            for (var i = 0; i < extensionMethods.Length; ++i) {
                var index = methods.Length + i;
                var method = extensionMethods[i];
                var variable = $"(({m_FullName})obj)";
                var pars = method.GetParameters();
                var fPars = new ParameterInfo[pars.Length - 1];
                for (var j = 1; j < pars.Length; ++j) { fPars[j - 1] = pars[j]; }
                var call = $"{variable}.{name}({GetScorpioMethodCall(fPars)})";
                string execute = GetExecuteMethod(fPars, method.ReturnType != typeof(void), call);
                builder.AppendFormat(@"
                case {0}: {{ {1} }}", index, execute);
                allMethodBuilder.Append($@"
            methodInfos.Add({GetScorpioMethod(false, fPars, index)});");
            }
            var str = MethodTemplate;
            str = str.Replace("__getallmethod", allMethodBuilder.ToString());
            str = str.Replace("__name", m_ScorpioClassName + "_" + name);
            str = str.Replace("__methodname", name);
            str = str.Replace("__execute", builder.ToString());
            return str;
        }
        //获取变量类型
        string GenerateGetVariableType() {
            var templateStr = @"
            case ""{0}"": return typeof({1});";
            var builder = new StringBuilder();
            //所有类变量
            m_Fields.ForEach((field) => builder.AppendFormat(templateStr, field.Name, ScorpioReflectUtil.GetFullName(field.FieldType)) );
            //所有属性
            m_Propertys.ForEach((property) => builder.AppendFormat(templateStr, property.Name, ScorpioReflectUtil.GetFullName(property.PropertyType)) );
            //所有的函数
            var methods = new List<string>();
            foreach (var method in m_Methods) {
                string name = method.Name;
                if (methods.Contains(name) || method.ReturnType == typeof(void)) { continue; }
                methods.Add(name);
                builder.AppendFormat(templateStr, method.Name, ScorpioReflectUtil.GetFullName(method.ReturnType));
            }
            return builder.ToString();
        }
        //获取函数
        string GenerateGetMethod() {
            var methodStr = @"
            case ""{0}"": return {1}.GetInstance();";
            var builder = new StringBuilder();
            foreach (var name in m_MethodNames) {
                builder.AppendFormat(methodStr, name, m_ScorpioClassName + "_" + name);
            }
            return builder.ToString();
        }
    }
}
