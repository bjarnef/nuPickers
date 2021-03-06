﻿
namespace nuPickers.Shared.XmlDataSource
{
    using nuPickers.Shared.Editor;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.XPath;
    using umbraco;
    using Umbraco.Core;

    public class XmlDataSource
    {
        public string XmlData { get; set; }

        public string Url { get; set; }

        public string XPath { get; set; }

        public string KeyXPath { get; set; }

        public string LabelXPath { get; set; }

        public IEnumerable<EditorDataItem> GetEditorDataItems(int currentId, int parentId)
        {
            XmlDocument xmlDocument;
            List<EditorDataItem> editorDataItems = new List<EditorDataItem>();

            switch (this.XmlData)
            {
                case "content":
                    xmlDocument = uQuery.GetPublishedXml(uQuery.UmbracoObjectType.Document);
                    break;

                case "media":
                    xmlDocument = uQuery.GetPublishedXml(uQuery.UmbracoObjectType.Media);
                    break;

                case "members":
                    xmlDocument = uQuery.GetPublishedXml(uQuery.UmbracoObjectType.Member);
                    break;

                case "url":
                    xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(Helper.GetDataFromUrl(this.Url));
                    break;

                default:
                    xmlDocument = null;
                    break;
            }

            if (xmlDocument != null)
            {
                string xPath = this.XPath;

                if (xPath.Contains("$ancestorOrSelf"))
                {
                    // default to 'self-or-parent-or-root'
                    int ancestorOrSelfId = currentId > 0 ? currentId : parentId > 0 ? parentId : -1;

                    // if we have a content id, but it's not published in the xml
                    if (this.XmlData == "content" && ancestorOrSelfId > 0 && xmlDocument.SelectSingleNode("/descendant::*[@id='" + ancestorOrSelfId + "']") == null)
                    {
                        // use Umbraco API to get path of all ids above ancestorOrSelfId to root
                        Queue<int> path = new Queue<int>(ApplicationContext.Current.Services.ContentService.GetById(ancestorOrSelfId).Path.Split(',').Select(x => int.Parse(x)).Reverse().Skip(1));

                        // find the nearest id in the xml
                        do { ancestorOrSelfId = path.Dequeue(); }
                        while (xmlDocument.SelectSingleNode("/descendant::*[@id='" + ancestorOrSelfId + "']") == null);
                    }

                    xPath = this.XPath.Replace("$ancestorOrSelf", string.Concat("/descendant::*[@id='", ancestorOrSelfId, "']"));
                }

                XPathNavigator xPathNavigator = xmlDocument.CreateNavigator();
                XPathNodeIterator xPathNodeIterator = xPathNavigator.Select(xPath);
                List<string> keys = new List<string>(); // used to keep track of keys, so that duplicates aren't added

                string key;
                string label;

                while (xPathNodeIterator.MoveNext())
                {
                    // media xml is wrapped in a <Media id="-1" /> node to be valid, exclude this from any results
                    // member xml is wrapped in <Members id="-1" /> node to be valid, exclude this from any results
                    if (xPathNodeIterator.CurrentPosition > 1 ||
                        !(xPathNodeIterator.Current.GetAttribute("id", string.Empty) == "-1" &&
                         (xPathNodeIterator.Current.Name == "Media" || xPathNodeIterator.Current.Name == "Members")))
                    {
                        key = xPathNodeIterator.Current.SelectSingleNode(this.KeyXPath).Value;

                        // only add item if it has a unique key - failsafe
                        if (!string.IsNullOrWhiteSpace(key) && !keys.Any(x => x == key))
                        {
                            // TODO: ensure key doens't contain any commas (keys are converted saved as csv)
                            keys.Add(key); // add key so that it's not reused

                            // set default markup to use the configured label XPath
                            label = xPathNodeIterator.Current.SelectSingleNode(this.LabelXPath).Value;

                            editorDataItems.Add(new EditorDataItem()
                            {
                                Key = key,
                                Label = label
                            });
                        }
                    }
                }
            }

            return editorDataItems;
        }
    }
}
