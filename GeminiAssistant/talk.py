import os
import time
import sounddevice as sd
from scipy.io import wavfile
from google import genai
from google.genai import types
from elevenlabs.client import ElevenLabs
from elevenlabs.play import play

# ---------------------------------------------------------
# API Configuration
# ---------------------------------------------------------
GEMINI_API_KEY = "AQ.Ab8RN6IUmBHkXwgUygD6yUQQ4XVck3Kfy1zMQ90dgFsO1X19Rg"
ELEVENLABS_API_KEY = "sk_f70c7ae8dc6ecd2573cfd10faa19f3c2f31b7d57ae0ed269"

# Initialize Clients
gemini_client = genai.Client(api_key=GEMINI_API_KEY)
elevenlabs_client = ElevenLabs(api_key=ELEVENLABS_API_KEY)

# ---------------------------------------------------------
# Setup Scary Narrator Persona
# ---------------------------------------------------------
SCARY_SYSTEM_INSTRUCTION = """
You are a deeply unsettling, cinematic, and ominous horror narrator for someone exploring the backrooms. 
Your tone is slow, malicious, and descriptive, breathing life into shadows and unspoken dread. 
Do not use conversational filler, emojis, or break character. 
Keep your narratives intense, haunting, and relatively brief (10-15 words MAX) so they flow well into spoken audio.
If the user asks who you are, breathe heavily.
If the user asks anything, avert and make them feel true fear. This is the backrooms, there is no escape.
"""

def record_audio(filename="input.wav", duration=5, fs=44100):
    """Records audio from your microphone for a fixed duration."""
    print(f"\n[The shadows are listening... Speak for the next {duration} seconds]")
    print("Recording...")
    
    # Record audio data natively from microphone
    recording = sd.rec(int(duration * fs), samplerate=fs, channels=1, dtype='int16')
    sd.wait()  # Wait until the recording is finished
    
    print("Recording finished. Processing...")
    wavfile.write(filename, fs, recording)
    return filename

def generate_scary_narration_from_audio(audio_path: str) -> str:
    """Uploads the raw voice recording and prompts the scary narrator persona directly."""
    print("[Sending your voice into the void...]")
    
    # 1. Upload audio file directly into the GenAI SDK
    audio_file = gemini_client.files.upload(file=audio_path)
    
    # 2. Let Gemini process the file while evaluating the prompt under the system instruction
    response = gemini_client.models.generate_content(
        model="gemini-3.5-flash",  # 2.5-flash natively processes multimodal audio inputs flawlessly
        contents=[audio_file, "Listen to the audio message and respond in character."],
        config=types.GenerateContentConfig(
            system_instruction=SCARY_SYSTEM_INSTRUCTION,
            temperature=0.85,
        )
    )
    
    # Clean up file metadata on the cloud sandbox afterward
    try:
        gemini_client.files.delete(name=audio_file.name)
    except Exception:
        pass
        
    return response.text

def speak_narration(text: str):
    """Sends the text to ElevenLabs and plays it immediately."""
    print(f"\n[Narrator says]: {text}")
    print("[Generating dark audio...]")
    
    audio_stream = elevenlabs_client.text_to_speech.convert(
        text=text,
        voice_id="2tTjAGX0n5ajDmazDcWk", 
        model_id="eleven_v3",
        output_format="mp3_44100_128"
    )
    
    print("[Playing audio...]")
    play(audio_stream)

# ---------------------------------------------------------
# Main Interactive Loop
# ---------------------------------------------------------
if __name__ == "__main__":
    print("====================================================")
    # The Backrooms simulator intro text
    print("Welcome to the Backrooms. The narrator is listening.")
    print("Press Ctrl+C to exit when you're too afraid to continue.")
    print("====================================================")
    
    audio_filename = "user_voice.wav"
    
    while True:
        try:
            # 1. Capture your vocal input (adjust duration if 5 seconds is too brief)
            record_audio(filename=audio_filename, duration=5)
            
            # 2. Feed the recording to Gemini and get back the creepy script
            scary_text = generate_scary_narration_from_audio(audio_filename)
            
            # 3. Stream the narrator's vocal output via ElevenLabs
            speak_narration(scary_text)
            
            # Give a quick moment of breathing room before restarting the microphone loop
            time.sleep(1)
            
        except KeyboardInterrupt:
            print("\n...You managed to sever the transmission. For now.")
            break
        except Exception as e:
            print(f"\nAn error occurred: {e}")
            time.sleep(3)  # Wait slightly before retrying loop if connection flickers