﻿using System;
using System.Text;
using System.Collections.Generic;
namespace Scorpio.ScorpioReflect {
    public partial class GenerateScorpioType {
        private const string Template = @"using System;
using Scorpio;
using Scorpio.Userdata;
namespace __Namespace {
    public class __ClassName {
        public static void Initialize(Script script) {
__CreateObject
        }
    }
}";
        public string Namespace = "ScorpioType";
        public string ClassName = "ScorpioTypeFactory";
        private List<Type> m_Types = new List<Type>();
        public void AddType(Type type) {
            if (!m_Types.Contains(type)) m_Types.Add(type);
        }
        public string Generate() {
            ScorpioReflectUtil.SortType(m_Types);
            string str = Template;
            str = str.Replace("__Namespace", string.IsNullOrEmpty(Namespace) ? "ScorpioDelegate" : Namespace);
            str = str.Replace("__ClassName", string.IsNullOrEmpty(ClassName) ? "ScorpioDelegateFactory" : ClassName);
            str = str.Replace("__CreateObject", CreateObject());
            return str;
        }
        public string CreateObject() {
            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (var type in m_Types) {
                if (first) { first = false; } else { builder.AppendLine(); }
                builder.AppendFormat("            script.SetObject(\"{0}\", script.CreateObject(typeof({0})));", ScorpioReflectUtil.GetFullName(type));
            }
            return builder.ToString();
        }
    }
}
