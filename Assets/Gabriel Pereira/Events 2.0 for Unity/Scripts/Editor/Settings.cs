using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

namespace UnityEditorInternal
{
	/// <summary>
	/// 
	/// </summary>
	public class Settings
	{
		private const string JsonFilePath = "Assets/Gabriel Pereira/Events 2.0 for Unity/settings.json";

		private static readonly string[] m_PredefinedAssemblies = new string[]
		{
			"mscorlib",
			"UnityEngine.CoreModule"
		};

		public static Events2Settings GetSettings()
		{
			CheckFileSettings();

			string content;
			using (StreamReader reader = new StreamReader(JsonFilePath))
			{
				content = reader.ReadToEnd();
			}

			if (string.IsNullOrWhiteSpace(content))
			{
				// A previous run may have left a truncated/corrupt file (e.g. CheckFileSettings
				// threw partway through writing). Regenerate it once before giving up.
				File.Delete(JsonFilePath);
				CheckFileSettings();
				content = File.ReadAllText(JsonFilePath);
			}

			Events2Settings settings = JsonUtility.FromJson<Events2Settings>(content);

			return settings;
		}

		private static void CheckFileSettings()
		{
			if (File.Exists(JsonFilePath)) return;

			using (StreamWriter writer = File.CreateText(JsonFilePath))
			{
				var settings = new Events2Settings();

				var solutionPath = GetSolutionPath();
				var content = File.ReadAllText(solutionPath);
				var projReg = new Regex("Project\\(\"\\{[\\w-]*\\}\"\\) = \"([\\w _]*.*)\", \"(.*\\.(cs|vcx|vb)proj)\"", RegexOptions.Compiled);
				var matches = projReg.Matches(content).Cast<Match>();
				var projects = matches.Select(x => x.Groups[2].Value).ToList();

				projects = projects
					.ConvertAll(project =>
					{
						var projectPath = project;
						if (!Path.IsPathRooted(projectPath))
							projectPath = Path.Combine(Path.GetDirectoryName(solutionPath), projectPath);

						return Path.GetFullPath(projectPath);
					});

				projects.Sort();

				settings.assemblies = projects
					.ConvertAll(projectPath =>
					{
						XDocument doc = XDocument.Load(projectPath);
						// MSBuild files often have a default namespace, which must be included in queries
						XNamespace ns = doc.Root.GetDefaultNamespace();

						// Helper function to get property value
						string GetPropertyValue(string propertyName)
						{
							// Look for the property within any PropertyGroup
							var property = doc.Descendants(ns + "PropertyGroup")
											  .Elements(ns + propertyName)
											  .FirstOrDefault();

							return property?.Value ?? "N/A";
						}

						if (GetPropertyValue("UnityProjectType").StartsWith("Editor"))
							return "N/A";

						return GetPropertyValue("AssemblyName");
					})
					.FindAll(assemblyName => assemblyName != "N/A")
					.ToArray();

				var assemblies = settings.assemblies;
				
				Array.Resize(ref assemblies, assemblies.Length + m_PredefinedAssemblies.Length);
				Array.Copy(m_PredefinedAssemblies, 0, assemblies, settings.assemblies.Length, m_PredefinedAssemblies.Length);
				
				settings.assemblies = assemblies;

				content = JsonUtility.ToJson(settings);

				writer.Write(content);
			}
		}

		private static string GetSolutionPath()
		{
			var currentDirPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			while (currentDirPath != null)
			{
				// Search for .sln files in the current directory. If multiple exist (e.g. a
				// leftover from a project rename), prefer the one matching the folder name,
				// otherwise fall back to the most recently written one.
				var solutionFiles = Directory.GetFiles(currentDirPath, "*.sln");

				if (solutionFiles.Length > 0)
				{
					var folderName = Path.GetFileName(currentDirPath);
					var solutionFileName = solutionFiles
						.Select(f => Path.GetFileName(f))
						.OrderByDescending(f => string.Equals(Path.GetFileNameWithoutExtension(f), folderName, StringComparison.InvariantCultureIgnoreCase))
						.ThenByDescending(f => File.GetLastWriteTimeUtc(Path.Combine(currentDirPath, f)))
						.First();

					return Path.Combine(currentDirPath, solutionFileName);
				}

				// Move up to the parent directory
				currentDirPath = Directory.GetParent(currentDirPath)?.FullName;
			}

			throw new FileNotFoundException("Cannot find solution file path");
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public struct Events2Settings
	{
		public string[] assemblies;
	}
}