﻿using System.ComponentModel.Design;
using Xpand.VSIX.Options;
using Xpand.VSIX.VSPackage;

namespace Xpand.VSIX.Commands{
    public class ShowOptionsCommand:VSCommand{
        private ShowOptionsCommand() : base((sender, args) => VSPackage.VSPackage.Instance.ShowOptionPage(typeof(OptionsPage)),new CommandID(PackageGuids.guidVSXpandPackageCmdSet,PackageIds.cmdidOptions)){
            BindCommand("Global::Alt+Shift+0");
        }

        public static void Init(){
            new ShowOptionsCommand();
        }
    }
}