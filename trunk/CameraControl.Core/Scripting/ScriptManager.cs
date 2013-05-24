﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using CameraControl.Core.Scripting.ScriptCommands;
using CameraControl.Devices;
using CameraControl.Devices.Classes;

namespace CameraControl.Core.Scripting
{
    public class ScriptManager : BaseFieldClass
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                NotifyPropertyChanged("IsBusy");
            }
        }


        public List<IScriptCommand> AvaiableCommands { get; set; }

        public ScriptManager()
        {
            AvaiableCommands = new List<IScriptCommand> {new BulbCapture()};
        }

        public void Save(ScriptObject scriptObject, string fileName)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(docNode);
            XmlNode rootNode = doc.CreateElement("ScriptObject");
            rootNode.Attributes.Append(CreateAttribute(doc, "UseExternal", scriptObject.UseExternal ? "true" : "false"));
            rootNode.Attributes.Append(CreateAttribute(doc, "SelectedConfig", scriptObject.SelectedConfig.Name));
            doc.AppendChild(rootNode);
            XmlNode commandsNode = doc.CreateElement("Commands");
            rootNode.AppendChild(commandsNode);
            foreach (IScriptCommand avaiableCommand in scriptObject.Commands)
            {
                commandsNode.AppendChild(avaiableCommand.Save(doc));
            }
            doc.Save(fileName);
        }

        public ScriptObject Load(string fileName)
        {
            ScriptObject res = new ScriptObject();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(fileName);
                XmlNode rootNode = doc.SelectSingleNode("/ScriptObject");
                if (GetValue(rootNode, "UseExternal") == "true")
                    res.UseExternal = true;
                res.SelectedConfig = ServiceProvider.Settings.DeviceConfigs.Get(GetValue(rootNode, "SelectedConfig"));
                XmlNode commandNode = doc.SelectSingleNode("/ScriptObject/Commands");
                foreach (XmlNode node in commandNode.ChildNodes)
                {
                    foreach (var command in AvaiableCommands)
                    {
                        if (command.Name == node.Name)
                            res.Commands.Add(command.Load(node));
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("Unable to load log file " + fileName, exception);
            }
            return res;
        }

        public static string GetValue(XmlNode node, string atribute)
        {
            if (node.Attributes != null && node.Attributes[atribute] == null)
                return "";
            return node.Attributes[atribute].Value;
        }

        public static XmlAttribute CreateAttribute(XmlDocument doc, string name, string val)
        {
            XmlAttribute attribute = doc.CreateAttribute(name);
            attribute.Value = val;
            return attribute;
        }

        public void Execute(ScriptObject scriptObject)
        {
            IsBusy = true;
            Thread thread=new Thread(ExecuteThread);
            thread.Start(scriptObject);
        }

        private void ExecuteThread(object o)
        {
            try
            {
                ScriptObject scriptObject = o as ScriptObject;
                foreach (IScriptCommand scriptCommand in scriptObject.Commands)
                {
                    scriptCommand.Execute(scriptObject);
                }
            }
            catch (Exception exception)
            {
                Log.Error("Error executing script", exception);
                StaticHelper.Instance.SystemMessage = exception.Message;
            }
            IsBusy = false;
        }
    }
}
