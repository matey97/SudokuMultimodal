using SudokuMultimodal.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace SudokuMultimodal
{
    public enum GrammarType { MOUSE_VOICE, ONLY_VOICE };
    public class SpeechRecognitionService
    {
        public event Action<SpeechRecognizedEventArgs> SpeechRecognized;

        private SpeechRecognitionEngine speechRecognizer;
        private SoundPlayer voiceOn, recognitionFailed;
        private Grammar mouseAndVoiceGrammar, onlyVoiceGrammar;

        private static SpeechRecognitionService speechRecognitionService;
        public static SpeechRecognitionService GetInstance()
        {
            if (speechRecognitionService == null)
                speechRecognitionService = new SpeechRecognitionService();
            return speechRecognitionService;
        }

        private SpeechRecognitionService()
        {
            speechRecognizer = new SpeechRecognitionEngine();
            speechRecognizer.SpeechRecognized += SpeechRecognizer_SpeechRecognized;
            speechRecognizer.SpeechRecognitionRejected += SpeechRecognizer_SpeechRecognitionRejected;
            speechRecognizer.SetInputToDefaultAudioDevice();

            voiceOn = new SoundPlayer(Resources.voice_on);
            recognitionFailed = new SoundPlayer(Resources.recog_failed);
        }

        private void SpeechRecognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (SpeechRecognized != null)
                SpeechRecognized(e);
        }

        private void SpeechRecognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {

            recognitionFailed.Play();
        }

        // Crea la gramática para Raton+Voz si es necesario
        private Grammar GetMouseAndVoiceGrammar()
        {
            if (mouseAndVoiceGrammar == null)
            {
                Choices grammarChoices = new Choices();
                for (var i = 1; i <= 9; i++)
                    grammarChoices.Add(i.ToString());
                grammarChoices.Add("Borrar");
                GrammarBuilder gb = new GrammarBuilder(grammarChoices);
                mouseAndVoiceGrammar = new Grammar(gb);
            }
            
            return mouseAndVoiceGrammar;
        }

        // Carga la gramática para Solo Voz si es necesario
        private Grammar GetOnlyVoiceGrammar()
        {
            if (onlyVoiceGrammar == null)
                onlyVoiceGrammar = new Grammar("sudoku_grammar.srgs");

            return onlyVoiceGrammar;
        }

        // Los clientes solicitan la gramática que necesitan
        public void SetGrammar(GrammarType grammarType)
        {
            speechRecognizer.UnloadAllGrammars();
            switch (grammarType)
            {
                case GrammarType.MOUSE_VOICE:
                    speechRecognizer.LoadGrammar(GetMouseAndVoiceGrammar());
                    break;
                case GrammarType.ONLY_VOICE:
                    speechRecognizer.LoadGrammar(GetOnlyVoiceGrammar());
                    break;
            }
        }

        public void RequestEnableRecognition()
        {
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
            voiceOn.Play();
        }

        public void RequestDisableRecognition()
        {
            speechRecognizer.RecognizeAsyncStop();
        }
    }
}
