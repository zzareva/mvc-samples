﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Controllers.Base;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields.BackendConfigurators;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields.MultipleChoiceField;
using Telerik.Sitefinity.Modules.Forms.Web.UI.Fields;
using Telerik.Sitefinity.Web.UI.Fields;

namespace SitefinityWebApp
{
    internal class MultipleChoiceFieldConfigurator : IFieldConfigurator
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
            var multipleChoiceControl = (FormMultipleChoice)webFormsControl;
            var multipleChoiceFieldModel = (IMultipleChoiceFieldModel)formFieldController.Model;
            var initialChoices = new List<string>();

            foreach (var choice in multipleChoiceControl.Choices)
            {
                initialChoices.Add(choice.Value);
            }

            multipleChoiceFieldModel.SerializedChoices = JsonConvert.SerializeObject(initialChoices);
        }
    }
}
