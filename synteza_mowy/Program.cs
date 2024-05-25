using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Wpisz tekst: ");
        string text = Console.ReadLine();
        SynthesizeSpeech(text);
    }

    static string NormalizePhrase(string phrase)
    {
        phrase = Regex.Replace(phrase, @"[^\w\s]", "");
        phrase = phrase.ToLower(new CultureInfo("pl-PL"));
        return string.Concat(phrase.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }

    static string FindAudioFile(string directory, string phrase)
    {
        string normalizedPhrase = NormalizePhrase(phrase);
        Console.WriteLine($"Normalizowana fraza: {normalizedPhrase}");

        foreach (var filePath in Directory.EnumerateFiles(directory))
        {
            string filename = Path.GetFileNameWithoutExtension(filePath);
            string normalizedFilename = NormalizePhrase(filename);
            Console.WriteLine($"Sprawdzanie pliku: {filename} (znormalizowany: {normalizedFilename})");

            if (normalizedFilename.Contains(normalizedPhrase))
            {
                string[] filenameParts = normalizedFilename.Split('_');
                string[] phraseParts = normalizedPhrase.Split(' ');

                if (filenameParts.Length == 2 && phraseParts.Length == 2)
                {
                    if (filenameParts[0] == phraseParts[0] && filenameParts[1] == phraseParts[1])
                    {
                        Console.WriteLine($"Znaleziono plik: {filename}");
                        return filePath;
                    }
                }
                else if (normalizedFilename == normalizedPhrase)
                {
                    Console.WriteLine($"Znaleziono plik: {filename}");
                    return filePath;
                }
            }
        }
        return null;
    }

    static void SynthesizeSpeech(string text)
    {
        string baseDir = "../../../../synteza_mowy/data";
        string stacjeDir = Path.Combine(baseDir, "stacje");
        string peronyIToryDir = Path.Combine(baseDir, "perony_i_tory");
        string doZStacjiDir = Path.Combine(baseDir, "do_z_stacji");

        var phrases = Regex.Split(text, @",|\s")
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Select(phrase => phrase.Trim())
            .ToList();
        var audioSegments = new List<AudioFileReader>();

        int i = 0;
        while (i < phrases.Count)
        {
            string phrase = phrases[i];
            string audioFile = null;
            Console.WriteLine($"Przetwarzanie frazy: {phrase}");

            if (phrase.Contains("stacji") || phrase.Contains("przez"))
            {
                if (i + 1 < phrases.Count)
                {
                    string combinedPhrase = $"{phrase} {phrases[i + 1]}";
                    audioFile = FindAudioFile(stacjeDir, combinedPhrase);
                    if (audioFile != null)
                    {
                        audioSegments.Add(new AudioFileReader(audioFile));
                        Console.WriteLine($"Dodano frazę: {combinedPhrase}");
                        i += 2;
                        continue;
                    }
                }

                audioFile = FindAudioFile(stacjeDir, phrase);
            }
            else if (phrase.Contains("peronie") || phrase.Contains("toru"))
            {
                audioFile = FindAudioFile(peronyIToryDir, phrase);
            }
            else
            {
                audioFile = FindAudioFile(doZStacjiDir, phrase);
            }

            if (audioFile != null)
            {
                audioSegments.Add(new AudioFileReader(audioFile));
                Console.WriteLine($"Dodano frazę: {phrase}");
            }
            else
            {
                Console.WriteLine($"Nie znaleziono pliku dla frazy: {phrase}");
            }

            i++;
        }

        if (audioSegments.Any())
        {
            var outputFile = Path.Combine(baseDir, "wynik.wav");
            using (var waveFileWriter = new WaveFileWriter(outputFile, audioSegments.First().WaveFormat))
            {
                foreach (var segment in audioSegments)
                {
                    segment.CopyTo(waveFileWriter);
                }
            }

            Console.WriteLine($"Plik wynikowy zapisany jako: {outputFile}");

            using (var audioFile = new AudioFileReader(outputFile))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(audioFile);
                outputDevice.Play();
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
        else
        {
            Console.WriteLine("Brak odpowiednich nagrań do syntezy.");
        }
    }
}
