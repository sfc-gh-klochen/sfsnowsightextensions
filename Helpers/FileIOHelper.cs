using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Snowflake.Powershell
{
    /// <summary>
    /// Helper functions for dealing with Folders and Files
    /// </summary>
    public class FileIOHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");

        #region Basic file and folder reading and writing

        public static bool CreateFolder(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    logger.Info("Creating folder {0}", folderPath);

                    Directory.CreateDirectory(folderPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Unable to create folder {0}", folderPath);
                logger.Error(ex);

                return false;
            }
        }

        public static bool CreateFolderForFile(string filePath)
        {
            return CreateFolder(Path.GetDirectoryName(filePath));
        }

        public static bool DeleteFolder(string folderPath)
        {
            int tryNumber = 1;

            do
            {
                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        logger.Info("Deleting folder {0}, try #{1}", folderPath, tryNumber);

                        Directory.Delete(folderPath, true);
                    }
                    return true;
                }
                catch (IOException ex)
                {
                    logger.Error(ex);
                    logger.Error("Unable to delete folder {0}", folderPath);

                    if (ex.Message.StartsWith("The directory is not empty"))
                    {
                        tryNumber++;
                        Thread.Sleep(3000);
                    }
                    else
                    {
                        return false;
                    }
                }
            } while (tryNumber <= 3);

            return true;
        }

        public static bool DeleteFile(string filePath)
        {
            int tryNumber = 1;

            do
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        logger.Info("Deleting file {0}, try #{1}", filePath, tryNumber);

                        File.Delete(filePath);
                    }
                    return true;
                }
                catch (IOException ex)
                {
                    logger.Error(ex);
                    logger.Error("Unable to delete file {0}", filePath);

                    tryNumber++;
                    Thread.Sleep(3000);
                }
            } while (tryNumber <= 3);

            return true;
        }

        public static bool CopyFolder(string folderPathSource, string folderPathTarget)
        {
            CreateFolder(folderPathTarget);

            foreach (string file in Directory.GetFiles(folderPathSource))
            {
                string dest = Path.Combine(folderPathTarget, Path.GetFileName(file));
                try
                {
                    logger.Info("Copying file {0} to {1}", file, dest);

                    File.Copy(file, dest, true);
                }
                catch (Exception ex)
                {
                    logger.Error("Unable to copy file {0} to {1}", file, dest);
                    logger.Error(ex);

                    return false;
                }
            }

            foreach (string folder in Directory.GetDirectories(folderPathSource))
            {
                string dest = Path.Combine(folderPathTarget, Path.GetFileName(folder));
                CopyFolder(folder, dest);
            }

            return true;
        }

        public static bool CopyFile(string filePathSource, string filePathDestination)
        {
            CreateFolderForFile(filePathDestination);

            try
            {
                logger.Info("Copying file {0} to {1}", filePathSource, filePathDestination);

                File.Copy(filePathSource, filePathDestination, true);
            }
            catch (Exception ex)
            {
                logger.Error("Unable to copy file {0} to {1}", filePathSource, filePathDestination);
                logger.Error(ex);

                return false;
            }

            return true;
        }

        public static bool SaveFileToPath(string fileContents, string filePath)
        {
            return SaveFileToPath(fileContents, filePath, true);
        }

        public static bool SaveFileToPath(string fileContents, string filePath, bool writeUTF8BOM)
        {
            string folderPath = Path.GetDirectoryName(filePath);

            if (CreateFolder(folderPath) == true)
            {
                try
                {
                    logger.Info("Writing string length {0} to file {1}", fileContents.Length, filePath);

                    if (writeUTF8BOM == true)
                    {
                        File.WriteAllText(filePath, fileContents, Encoding.UTF8);
                    }
                    else
                    {
                        Encoding utf8WithoutBom = new UTF8Encoding(false);
                        File.WriteAllText(filePath, fileContents, utf8WithoutBom);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("Unable to write to file {0}", filePath);
                    logger.Error(ex);
                }
            }

            return false;
        }

        public static TextWriter SaveFileToPathWithWriter(string filePath)
        {
            string folderPath = Path.GetDirectoryName(filePath);

            if (CreateFolder(folderPath) == true)
            {
                try
                {
                    logger.Info("Opening TextWriter to file {0}", filePath);

                    return File.CreateText(filePath);
                }
                catch (Exception ex)
                {
                    logger.Error("Unable to write to file {0}", filePath);
                    logger.Error(ex);

                    return null;
                }
            }
            return null;
        }

        public static string ReadFileFromPath(string filePath)
        {
            try
            {
                if (File.Exists(filePath) == false)
                {
                    logger.Warn("Unable to find file {0}", filePath);
                }
                else
                {
                    logger.Info("Reading file {0}", filePath);
                    return File.ReadAllText(filePath, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Unable to read from file {0}", filePath);
                logger.Error(ex);
            }

            return String.Empty;
        }

        #endregion

        #region JSON file reading and writing

        public static JObject LoadJObjectFromFile(string jsonFilePath)
        {
            try
            {
                if (File.Exists(jsonFilePath) == false)
                {
                    logger.Warn("Unable to find file {0}", jsonFilePath);
                }
                else
                {
                    logger.Info("Reading JObject from file {0}", jsonFilePath);

                    return JObject.Parse(File.ReadAllText(jsonFilePath));
                }
            }
            catch (Exception ex)
            {
                logger.Error("Unable to load JSON from file {0}", jsonFilePath);
                logger.Error(ex);
            }

            return null;
        }

        public static bool WriteObjectToFile(object objectToWrite, string jsonFilePath)
        {
            string folderPath = Path.GetDirectoryName(jsonFilePath);

            if (CreateFolder(folderPath) == true)
            {
                try
                {
                    logger.Info("Writing object {0} to file {1}", objectToWrite.GetType().Name, jsonFilePath);

                    using (StreamWriter sw = File.CreateText(jsonFilePath))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.NullValueHandling = NullValueHandling.Include;
                        serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
                        serializer.Serialize(sw, objectToWrite);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("Unable to write object to file {0}", jsonFilePath);
                    logger.Error(ex);
                }
            }

            return false;
        }

        public static JArray LoadJArrayFromFile(string jsonFilePath)
        {
            try
            {
                if (File.Exists(jsonFilePath) == false)
                {
                    logger.Warn("Unable to find file {0}", jsonFilePath);
                }
                else
                {
                    logger.Info("Reading JArray from file {0}", jsonFilePath);

                    return JArray.Parse(File.ReadAllText(jsonFilePath));
                }
            }
            catch (Exception ex)
            {
                logger.Error("Unable to load JSON from file {0}", jsonFilePath);
                logger.Error(ex);
            }

            return null;
        }

        public static bool WriteJArrayToFile(JArray array, string jsonFilePath)
        {
            logger.Info("Writing JSON Array with {0} elements to file {1}", array.Count, jsonFilePath);

            return WriteObjectToFile(array, jsonFilePath);
        }

        public static List<T> LoadListOfObjectsFromFile<T>(string jsonFilePath)
        {
            try
            {
                if (File.Exists(jsonFilePath) == false)
                {
                    logger.Warn("Unable to find file {0}", jsonFilePath);
                }
                else
                {
                    logger.Info("Reading List<{0}> from file {1}", typeof(T), jsonFilePath);

                    return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(jsonFilePath));
                }
            }
            catch (Exception ex)
            {
                logger.Error("Unable to load JSON from file {0}", jsonFilePath);
                logger.Error(ex);
            }

            return null;
        }

        #endregion

        #region File name handling

        public static string GetFileSystemSafeString(string fileOrFolderNameToClear)
        {
            if (fileOrFolderNameToClear == null) fileOrFolderNameToClear = String.Empty;

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileOrFolderNameToClear = fileOrFolderNameToClear.Replace(c, '-');
            }

            return fileOrFolderNameToClear;
        }

        public static string GetShortenedEntityNameForFileSystem(string entityName, int maxLength)
        {
            string originalEntityName = entityName;

            // First, strip out unsafe characters
            entityName = GetFileSystemSafeString(entityName);

            // Second, shorten the string 
            if (entityName.Length > maxLength) entityName = entityName.Substring(0, maxLength);

            return entityName;
        }

        #endregion
    }
}
