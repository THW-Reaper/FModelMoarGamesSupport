using csharp_wick;
using FModel.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace FModel
{
    static class JohnWick
    {
        public static PakAsset MyAsset;
        private static PakExtractor _myExtractor;
        public static string[] myArray { get; set; }
        private static string currentPakToCheck { get; set; }

        /// <summary>
        /// Normal pak file: using AllpaksDictionary, it tells you the pak name depending on currentItem. Using this pak name and PaksMountPoint we get the mount point
        /// </summary>
        /// <param name="currentItem"></param>
        /// <returns> the mount point as string, used to create subfolders when extracting or create the tree when loading all paks </returns>
        private static string GetMountPointFromDict(string currentItem)
        {
            return ThePak.PaksMountPoint[ThePak.AllpaksDictionary[currentItem]];
        }

        /// <summary>
        /// just the method to create subfolders using GetMountPointFromDict and write the file from myResults with its byte[] data
        /// </summary>
        /// <param name="currentItem"></param>
        /// <param name="myResults"></param>
        /// <param name="data"></param>
        /// <returns> the path to this brand new created file </returns>
        private static string WriteFile(string currentItem, string myResults, byte[] data)
        {
            Directory.CreateDirectory(App.DefaultOutputPath + "\\Extracted\\" + GetMountPointFromDict(currentItem) + myResults.Substring(0, myResults.LastIndexOf("/", StringComparison.Ordinal)));
            File.WriteAllBytes(App.DefaultOutputPath + "\\Extracted\\" + GetMountPointFromDict(currentItem) + myResults, data);

            return App.DefaultOutputPath + "\\Extracted\\" + GetMountPointFromDict(currentItem) + myResults;
        }

        /// <summary>
        /// We get the file list of currentPak, we find all files that matched our currentItem, for each result we get its index (it's used to get its data)
        /// Then we can use WriteFile to write each results with its data
        /// If currentPak is the same twice in a row, we do not try to get a new file list
        /// </summary>
        /// <param name="currentPak"></param>
        /// <param name="currentItem"></param>
        /// <returns> the path of the last created file (usually the uexp file but we don't care about the extension, so it's fine) </returns>
        public static string ExtractAsset(string currentPak, string currentItem)
        {
            string pakGuid = ThePak.dynamicPaksList.Where(x => x.thePak == currentPak).Select(x => x.thePakGuid).FirstOrDefault();
            string myKey = string.Empty;
            if (!string.IsNullOrEmpty(pakGuid) && pakGuid != "0-0-0-0")
            {
                myKey = DynamicKeysManager.AESEntries.Where(x => x.thePak == currentPak).Select(x => x.theKey).FirstOrDefault();
            }
            else { myKey = Settings.Default.AESKey; }

            if (currentPak != currentPakToCheck || myArray == null)
            {
                _myExtractor = new PakExtractor(Settings.Default.PAKsPath + "\\" + currentPak, myKey);
                myArray = _myExtractor.GetFileList().ToArray();
            }

            string[] results = currentItem.Contains(".") ? Array.FindAll(myArray, s => s.Contains("/" + currentItem)) : Array.FindAll(myArray, s => s.Contains("/" + currentItem + "."));

            string AssetPath = string.Empty;
            for (int i = 0; i < results.Length; i++)
            {
                int index = Array.IndexOf(myArray, results[i]);

                uint y = (uint)index;
                byte[] b = _myExtractor.GetData(y);

                AssetPath = WriteFile(currentItem, results[i], b).Replace("/", "\\");
            }

            currentPakToCheck = currentPak;
            return AssetPath;
        }

        /// <summary>
        /// just convert the asset to a png image with some exceptions in case AssetName is a material
        /// </summary>
        /// <param name="AssetName"></param>
        /// <returns> the path to the png image </returns>
        public static string AssetToTexture2D(string AssetName)
        {
            string textureFilePath = ExtractAsset(ThePak.AllpaksDictionary[AssetName], AssetName);
            string TexturePath = string.Empty;
            if (!string.IsNullOrEmpty(textureFilePath))
            {
                if (textureFilePath.Contains("MI_UI_FeaturedRenderSwitch_") || textureFilePath.Contains("M_UI_ChallengeTile_PCB") || textureFilePath.Contains("Wraps\\FeaturedMaterials\\") || textureFilePath.Contains("M-Wraps-StreetDemon"))
                    return GetRenderSwitchMaterialTexture(textureFilePath);
                else
                {
                    MyAsset = new PakAsset(textureFilePath.Substring(0, textureFilePath.LastIndexOf(".", StringComparison.Ordinal)));
                    MyAsset.SaveTexture(textureFilePath.Substring(0, textureFilePath.LastIndexOf(".", StringComparison.Ordinal)) + ".png");
                    TexturePath = textureFilePath.Substring(0, textureFilePath.LastIndexOf(".", StringComparison.Ordinal)) + ".png";
                }
            }

            return TexturePath;
        }

        /// <summary>
        /// serialize the material to get the texture and convert this texture to a png image
        /// </summary>
        /// <param name="AssetPath"></param>
        /// <returns> the path to the png image </returns>
        public static string GetRenderSwitchMaterialTexture(string AssetPath)
        {
            string TexturePath = string.Empty;
            if (AssetPath.Contains(".uasset") || AssetPath.Contains(".uexp") || AssetPath.Contains(".ubulk"))
            {
                MyAsset = new PakAsset(AssetPath.Substring(0, AssetPath.LastIndexOf('.')));
                try
                {
                    if (MyAsset.GetSerialized() != null)
                    {
                        dynamic AssetData = JsonConvert.DeserializeObject(MyAsset.GetSerialized());
                        JArray AssetArray = JArray.FromObject(AssetData);

                        JToken textureParameterValues = AssetArray[0]["TextureParameterValues"];
                        if (textureParameterValues != null)
                        {
                            JArray textureParameterValuesArray = textureParameterValues.Value<JArray>();
                            foreach (JToken token in textureParameterValuesArray)
                            {
                                JToken parameterInfo = token["ParameterInfo"];
                                if (parameterInfo != null)
                                {
                                    JToken name = parameterInfo["Name"];
                                    if (name != null && name.Value<string>().Equals("TextureB"))
                                    {
                                        JToken parameterValue = token["ParameterValue"];
                                        if (parameterValue != null)
                                        {
                                            string textureFile = parameterValue.Value<string>();
                                            TexturePath = AssetToTexture2D(textureFile);
                                        }
                                    }
                                    else if (name != null && name.Value<string>().Contains("Texture"))
                                    {
                                        JToken parameterValue = token["ParameterValue"];
                                        if (parameterValue != null)
                                        {
                                            string textureFile = parameterValue.Value<string>();
                                            TexturePath = AssetToTexture2D(textureFile);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (JsonSerializationException)
                {
                    //do not crash when JsonSerialization does weird stuff
                }
            }
            return TexturePath;
        }
    }
}
