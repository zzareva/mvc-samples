﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Controllers.Base;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields.BackendConfigurators;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields.DropdownListField;
using Telerik.Sitefinity.Modules.Forms.Web.UI.Fields;
using Telerik.Sitefinity.Web.UI.Fields;

namespace SitefinityWebApp
{
    internal class DropdownFieldConfigurator: IFieldConfigurator
    {
        /// <inheritDocs/>
        public Guid FormId
        {
            get;
            set;
        }

        /// <inheritDocs/>
        public void Configure(FieldControl webFormsControl, IFormFieldController<IFormFieldModel> formFieldController)
        {
            var dropdownControl = (FormDropDownList)webFormsControl;
            var dropdownFieldModel = (IDropdownListFieldModel)formFieldController.Model;
            var initialChoices = new List<string>();

            foreach (var choice in dropdownControl.Choices)
            {
                initialChoices.Add(choice.Value);
            }

            dropdownFieldModel.SerializedChoices = JsonConvert.SerializeObject(initialChoices);
        }
    }
}
