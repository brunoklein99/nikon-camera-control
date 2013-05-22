﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Xml;

namespace CameraControl.Core.Scripting.ScriptCommands
{
    class BulbCapture : IScriptCommand
    {
        #region Implementation of IScriptCommand

        public bool Execute()
        {
            return true;
        }

        public XmlNode Save()
        {
            return null;
        }

        public void Load(XmlNode node)
        {
            
        }

        public bool IsExecuted { get; set; }

        public bool Executing { get; set; }

        public string Name { get; set; }

        public string DisplayName
        {
            get { return string.Format("[{0}]", Name); }
            set { }
        }

        public UserControl GetConfig()
        {
            return null;
        }

        #endregion

        public BulbCapture()
        {
            Name = "BulbCapture";
            IsExecuted = false;
            Executing = false;
        }
    }
}
