﻿using System;
using System.Runtime.Serialization;
using AntiPlagiarism.Api.ModelBinders;
using Microsoft.AspNetCore.Mvc;
using Ulearn.Common.Api.Models.Parameters;

namespace AntiPlagiarism.Api.Models.Parameters
{
	[DataContract]
	[ModelBinder(typeof(JsonModelBinder), Name = "parameters")]
	public class SetSuspicionLevelsParameters : ApiParameters
	{
		[DataMember(Name = "task_id", IsRequired = true)]
		public Guid TaskId { get; set; }

		[DataMember(Name = "faint_suspicion")]
		public double? FaintSuspicion { get; set; }

		[DataMember(Name = "strong_suspicion")]
		public double? StrongSuspicion { get; set; }
	}
}