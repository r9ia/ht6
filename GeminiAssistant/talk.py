import os
from google import genai
from google.genai import types
from elevenlabs.client import ElevenLabs
from elevenlabs.play import play

# ---------------------------------------------------------
# API Configuration (Fill these in or use Environment Variables)
# ---------------------------------------------------------
GEMINI_API_KEY = "AQ.Ab8RN6Lo_KV5FVRG82FJ94oGKL59Csrlcx5YseNDh-a3E6NtUw"
ELEVENLABS_API_KEY = "sk_f70c7ae8dc6ecd2573cfd10faa19f3c2f31b7d57ae0ed269"

# Fallback to environment variables if keys aren't manually specified above
if GEMINI_API_KEY == "YOUR_GEMINI_API_KEY":
    GEMINI_API_KEY = os.environ.get("GEMINI_API_KEY")
if ELEVENLABS_API_KEY == "YOUR_ELEVENLABS_API_KEY":
    ELEVENLABS_API_KEY = os.environ.get("ELEVENLABS_API_KEY")

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
Keep your narratives intense, haunting, and relatively brief (1-3 sentences) so they flow well into spoken audio.
If the user asks who you are, breathe heavily.
If the user asks anything, avert and make them feel true fear. This is the backrooms, there is no escape.
"""

def generate_scary_narration(prompt: str) -> str:
    """Generates creepy text from Gemini using a system instruction."""
    print(f"\n[Prompting the Dark...] '{prompt}'")
    
    response = gemini_client.models.generate_content(
        model="gemini-2.5-flash",
        contents=prompt,
        config=types.GenerateContentConfig(
            system_instruction=SCARY_SYSTEM_INSTRUCTION,
            temperature=0.85,  # Slightly higher for more creative/creepy word choice
        )
    )
    return response.text

def speak_narration(text: str):
    """Sends the text to ElevenLabs and plays it immediately."""
    print(f"[Narrator says]: {text}")
    print("[Generating dark audio...]")
    
    # You can change the voice_id to any voice in your ElevenLabs library.
    # "CWhvzy5XvH468SIvRlXf" is a default ominous/deep voice placeholder (like 'Michael').
    audio_stream = elevenlabs_client.text_to_speech.convert(
        text=text,
        voice_id="CWhvzy5XvH468SIvRlXf", 
        model_id="eleven_v3",
        output_format="mp3_44100_128"
    )
    
    print("[Playing audio...]")
    play(audio_stream)

# ---------------------------------------------------------
# Execution Example
# ---------------------------------------------------------
if __name__ == "__main__":
    # Test prompt to spark a mini-horror script
    user_prompt = "What happens when the campfire goes out in the middle of these woods?"
    
    try:
        # 1. Get the scary text from Gemini
        scary_text = generate_scary_narration(user_prompt)
        
        # 2. Feed it into ElevenLabs to voice it
        speak_narration(scary_text)
        
    except Exception as e:
        print(f"\nAn error occurred. Check your API keys and configuration.\nDetails: {e}")