using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using TensorStack.Common.Common;

namespace Amuse.App
{
    public static class Json
    {
        public readonly static JsonSerializerOptions DefaultOptions;

        static Json()
        {
            DefaultOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }


        public static T Load<T>(string filePath) where T : class
        {
            try
            {
                using (var jsonReader = File.OpenRead(filePath))
                {
                    return JsonSerializer.Deserialize<T>(jsonReader, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("[Json] [Load] An exception occurred loading JSON file.\n\tFile: {filePath}\n\tError: {message}", filePath, ex.Message);
                return default;
            }
        }


        public static async Task<T> LoadAsync<T>(string filePath) where T : class
        {
            try
            {
                using (var jsonReader = File.OpenRead(filePath))
                {
                    return await JsonSerializer.DeserializeAsync<T>(jsonReader, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("[Json] [LoadAsync] An exception occurred loading JSON file.\n\tFile: {filePath}\n\tError: {message}", filePath, ex.Message);
                return default;
            }
        }


        public static async Task<T[]> LoadArrayAsync<T>(string modelFile) where T : class
        {
            var singleModel = await DeserializeAsync<T>(modelFile);
            if (singleModel != null)
                return [singleModel];

            return await DeserializeAsync<T[]>(modelFile);
        }


        public static void Save<T>(string filePath, T obj)
        {
            try
            {
                var type = GetObjType<T>(obj);
                using (var jsonWriter = File.OpenWrite(filePath))
                {
                    JsonSerializer.Serialize(jsonWriter, obj, type, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("[Json] [Save] An exception occurred saving JSON file.\n\tFile: {filePath}\n\tError: {message}", filePath, ex.Message);
            }
        }


        public static async Task SaveAsync<T>(string filePath, T obj)
        {
            var temp = filePath + ".tmp";
            try
            {
                var type = GetObjType<T>(obj);
                await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, obj, type, DefaultOptions);
                }
                File.Move(temp, filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Logger.Error("[Json] [SaveAsync] An exception occurred saving JSON file.\n\tFile: {filePath}\n\tError: {message}", filePath, ex.Message);
            }
            finally
            {
                FileHelper.DeleteFile(temp);
            }
        }


        private static async Task<T> DeserializeAsync<T>(string filePath)
        {
            try
            {
                using (var jsonReader = File.OpenRead(filePath))
                {
                    return await JsonSerializer.DeserializeAsync<T>(jsonReader, DefaultOptions);
                }
            }
            catch { return default; }

        }


        private static Type GetObjType<T>(T obj)
        {
            var type = typeof(T);
            if (type.IsInterface)
                return obj.GetType();

            return type;
        }
    }
}
