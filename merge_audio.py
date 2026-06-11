#!/usr/bin/env python3
import re
import subprocess

# Read main scene
with open('Assets/Scenes/SampleScene.unity', 'r', encoding='utf-8') as f:
    main_scene = f.read()

# Get Sapuni scene
result = subprocess.run(['git', 'show', 'Sapuni:Assets/Scenes/SampleScene.unity'], 
                       capture_output=True, text=True, cwd='.')
sapuni_scene = result.stdout

# Find the BackgroundMusic GameObject and Transform sections
# Pattern to capture the entire BackgroundMusic definition
pattern = r'(--- !u!1 &259390850[\s\S]*?)(?=\n--- !u!|$)'
match = re.search(pattern, sapuni_scene)

if match:
    audio_yaml = match.group(1)
    
    # Find where to insert - right before the last RectTransform or just before the end
    # We'll insert it at the end, before the last line
    if main_scene.endswith('\n'):
        insertion_point = main_scene.rfind('\n', 0, -1)  # Find second to last newline
    else:
        insertion_point = len(main_scene)
    
    # Insert the audio section
    new_scene = main_scene[:insertion_point] + '\n' + audio_yaml + '\n' + main_scene[insertion_point:]
    
    # Write back
    with open('Assets/Scenes/SampleScene.unity', 'w', encoding='utf-8') as f:
        f.write(new_scene)
    
    print("✓ Added BackgroundMusic GameObject to scene")
else:
    print("✗ Could not find BackgroundMusic in Sapuni scene")
