using Flwr.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Flwr
{
	public class XmlTransformer
	{
		public delegate void SubtreeHandler(XmlTransformer xform, XmlReader subtree, string elementPath, XmlWriter output);

		const string FlwrSource = "flwr.src";

		readonly IFileInput input;
		readonly IFileOutput output;

		public XmlTransformer(IFileInput input, IFileOutput output)
		{
			this.input = input;
			this.output = output;
		}

		public void ExternalizeElements(string inputName, string elementName, string elementPath, SubtreeHandler handleSubtree = null)
		{
			using (var xml = CreateReader(inputName))
			using (var o = CreateWriter(inputName))
				ExternalizeElements(xml, elementName, n => Path.Combine(elementPath, $"{n}.xml"), o, handleSubtree);
		}

		public void InternalizeElements(string inputName)
		{
			bool InternSource(XmlReader src, XmlWriter dst)
			{
				if (!src.IsEmptyElement || !src.HasAttributes)
					return false;
				var source = src.GetAttribute(FlwrSource);
				if (string.IsNullOrEmpty(source))
					return false;
				using (var subtree = CreateReader(source))
					CopyNodes(subtree, dst, InternSource);
				return true;
			}

			using (var xml = CreateReader(inputName))
			using (var o = CreateWriter(inputName))
				CopyNodes(xml, o, InternSource);
		}

		XmlReader CreateReader(string path) =>
			XmlReader.Create(input.OpenText(path));

		XmlWriter CreateWriter(string path) =>
			XmlWriter.Create(output.CreateText(path), new XmlWriterSettings
			{
				Indent = true,
				CloseOutput = true,
				ConformanceLevel = ConformanceLevel.Auto,
			});

		public void ExternalizeElements(XmlReader xml, string elementName, Func<int, string> getOutputName, XmlWriter b, SubtreeHandler handleSubtree = null)
		{
			var elementNumber = 0;
			while (xml.Read())
			{
				if (xml.NodeType == XmlNodeType.Element && xml.LocalName == elementName)
				{
					var elementPath = getOutputName(++elementNumber);
					using (var output = CreateWriter(elementPath))
					{
						var subtree = xml.ReadSubtree();
						if (handleSubtree != null)
							handleSubtree(this, subtree, elementPath, output);
						else CopyNodes(subtree, output);
					}
					b.WriteStartElement(elementName);
					b.WriteAttributeString(FlwrSource, elementPath);
					b.WriteEndElement();
				}
				else CopyNode(xml, b);
			}
		}

		static void CopyNodes(XmlReader src, XmlWriter dst, Func<XmlReader, XmlWriter, bool> handleElement = null)
		{
			while (src.Read())
				if (handleElement == null
				|| src.NodeType != XmlNodeType.Element
				|| !handleElement(src, dst))
					CopyNode(src, dst);
		}

		static void CopyNode(XmlReader src, XmlWriter dst)
		{
			switch (src.NodeType)
			{
				default:
					throw new NotImplementedException($"{src.NodeType}");
				case XmlNodeType.XmlDeclaration:
					dst.WriteStartDocument();
					break;
				case XmlNodeType.Comment:
					dst.WriteComment(src.Value);
					break;
				case XmlNodeType.Element:
					dst.WriteStartElement(src.LocalName);
					if (src.HasAttributes)
						dst.WriteAttributes(src, true);
					if (src.IsEmptyElement)
						goto case XmlNodeType.EndElement;
					break;
				case XmlNodeType.EndElement:
					dst.WriteEndElement();
					break;
				case XmlNodeType.Whitespace:
					break;
				case XmlNodeType.Text:
					dst.WriteString(src.Value);
					break;
			}
		}
	}
}
