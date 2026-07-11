using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// 音楽ファイルからタグ情報を読み取るインターフェイス。
    /// </summary>
    public interface ITagReader
    {
        /// <summary>
        /// 指定ファイルのタグ情報を読み取る。
        /// </summary>
        /// <param name="filePath">音楽ファイルの絶対パス。</param>
        /// <returns>読み取ったタグ情報。</returns>
        /// <exception cref="Exception">タグが読めない／壊れている場合にスローされる。</exception>
        MusicFileInfo Read(string filePath);
    }
}
