using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class RequestAndPresentRefactoringPayload
{
    public string FileName { get; set; }
    public FnData Fn { get; set; }
}
