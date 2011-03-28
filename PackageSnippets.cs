using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Snippets
{
	public class PackageSnippets
	{
		private readonly string rootdir;

		public PackageSnippets()
		{
			rootdir =
				new DirectoryInfo(new Uri(Path.GetDirectoryName(GetType().Assembly.CodeBase)).AbsolutePath).Parent.Parent.FullName;
			RemoveFiles();
			CreateVSContentFile();
			PackageVSIFile();
			CreateHTMLDocumentation();
		}

		public static void Main(string[] args)
		{
			new PackageSnippets();
		}

		private void RemoveFiles()
		{
			DeleteIfExists("jssnippets.vscontent");
			DeleteIfExists("jssnippets.vsi");
		}

		private void DeleteIfExists(string filename)
		{
			filename = GetFileFullName(filename);
			if (File.Exists(filename))
			{
				File.Delete(filename);
			}
		}

		private void CreateVSContentFile()
		{
			string template = File.ReadAllText(GetFileFullName("jssnippets.vscontenttemplate"));

			string vscontent = template.Replace("${SNIPPET_FILES}",
			                                    String.Join("\n\t\t\t\t",
			                                                GetListOfSnippetFiles().Select(
			                                                	f => "<FileName>" + new FileInfo(f).Name + "</FileName>")));
			File.WriteAllText(GetFileFullName("jssnippets.vscontent"), vscontent, Encoding.UTF8);
		}

		private void PackageVSIFile()
		{
			using (Package zip = Package.Open(GetFileFullName("jssnippets.vsi"), FileMode.Create))
			{
				foreach (string f in GetListOfSnippetFiles())
				{
					AddFileToPackage(zip, f);
				}
				AddFileToPackage(zip, GetFileFullName("jssnippets.vscontent"));
			}
		}


		private static void AddFileToPackage(Package zip, string f)
		{
			PackagePart p = zip.CreatePart(
				PackUriHelper.CreatePartUri(new Uri(new FileInfo(f).Name, UriKind.RelativeOrAbsolute)), MediaTypeNames.Text.Plain);
			using (var fileStream = new FileStream(f, FileMode.Open, FileAccess.Read))
			{
				CopyStream(fileStream, p.GetStream());
				// zip.CreateRelationship(p.Uri, TargetMode.Internal, f);
			}
		}

		private static void CopyStream(Stream source, Stream target)
		{
			const int bufSize = 0x1000;
			var buf = new byte[bufSize];
			int bytesRead;
			while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
				target.Write(buf, 0, bytesRead);
		}

		private void CreateHTMLDocumentation()
		{
			string template = File.ReadAllText(GetFileFullName("doctemplate.html"));
			string doccontent = template.Replace("${SNIPPET_ROW}",
			                                     String.Join("\n\t\t\t\t",
			                                                 GetListOfSnippetFiles().Select(
			                                                 	f => GetSnippetHtmlDescription(f) + "</FileName>")));
			File.WriteAllText(GetFileFullName("doc.html"), doccontent, Encoding.UTF8);
		}

		private static string GetSnippetHtmlDescription(string snippet)
		{
			XPathDocument doc = new XPathDocument(snippet, XmlSpace.Preserve);						
			XPathNavigator nav = doc.CreateNavigator();
			XmlNamespaceManager ns = new XmlNamespaceManager(nav.NameTable);
			ns.AddNamespace("ns", "http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet");

			XPathExpression literals = CreateExpression(nav, "/ns:CodeSnippet/ns:Snippet/ns:Declarations/ns:Literal", ns);
			
			StringBuilder htmlrow = new StringBuilder("<tr><td title='");
			htmlrow.Append(GetStringValueFromExpression(nav, ns, "/ns:CodeSnippet/ns:Header/ns:Description")).Append("'>")
				.Append(GetStringValueFromExpression(nav, ns, "/ns:CodeSnippet/ns:Header/ns:Title")).Append("</td><td><pre>")
				.Append(GetStringValueFromExpression(nav, ns, "/ns:CodeSnippet/ns:Snippet/ns:Code")).Append("</pre></td><td>")
				.Append(GetSnippetLiteralsHtmlDescription(nav.Select(literals), ns)).Append("</td></tr>");
			
			return htmlrow.ToString();
		}

		private static string GetSnippetLiteralsHtmlDescription(XPathNodeIterator literals, XmlNamespaceManager ns)
		{
			StringBuilder literalshtml = new StringBuilder("<ul>");
			while (literals.MoveNext())
			{
				XPathNavigator nav = literals.Current;
				literalshtml.Append("<li>").Append(GetSnippetLiteralHtmlDescription(nav, ns)).Append("</li>");
			}
			return literalshtml.ToString();
		}

		private static string GetSnippetLiteralHtmlDescription(XPathNavigator nav, XmlNamespaceManager ns)
		{
			return GetStringValueFromExpression(nav, ns, "ns:ID") + ": " + GetStringValueFromExpression(nav, ns, "ns:ToolTip");
		}

		private static string GetStringValueFromExpression(XPathNavigator nav, XmlNamespaceManager ns, string xpath)
		{
			XPathExpression expr = CreateExpression(nav, xpath, ns);
			XPathNodeIterator it = nav.Select(expr);
			return it.MoveNext() ? it.Current.Value : "";
		}

		private static XPathExpression CreateExpression(XPathNavigator nav, string xpath, XmlNamespaceManager ns)
		{
			XPathExpression expr = nav.Compile(xpath);			
			expr.SetContext(ns);
			return expr;
		}

		private IEnumerable<String> GetListOfSnippetFiles() { return Directory.GetFiles(GetFileFullName("snippets")); }

		private string GetFileFullName(string filename) { return rootdir + "\\" + filename; }
	}
}