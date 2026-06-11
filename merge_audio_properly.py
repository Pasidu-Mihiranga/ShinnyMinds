#!/usr/bin/env python3
"""
Merge audio GameObjects from Sapuni branch into main branch scene,
while preserving all NPCs and other content from main.
"""

import subprocess
import re

def get_file_from_git(branch, filepath):
    """Get file content from a specific git branch."""
    result = subprocess.run(
        ['git', 'show', f'{branch}:{filepath}'],
        capture_output=True,
        text=True,
        cwd='.'
    )
    if result.returncode != 0:
        raise Exception(f"Failed to get {filepath} from {branch}: {result.stderr}")
    return result.stdout

def extract_gameobject(content, name):
    """Extract a GameObject and its components from Unity scene YAML."""
    # Find the GameObject definition
    pattern = f'(?:--- !u!1 &\\d+\n.*?m_Name: {re.escape(name)}.*?)(?=\n--- !u!|$)'
    
    # More comprehensive pattern for a complete GameObject and all its components
    # Look for GameObject marker (--- !u!1 &...) through all associated components
    lines = content.split('\n')
    result_lines = []
    in_target = False
    gameobject_id = None
    
    for i, line in enumerate(lines):
        # Look for the target GameObject
        if not in_target and f'm_Name: {name}' in line:
            # Go backwards to find the GameObject definition
            for j in range(i, -1, -1):
                if lines[j].startswith('--- !u!1 &'):
                    in_target = True
                    gameobject_id = lines[j].split('&')[1].strip()
                    # Start collecting from this line
                    result_lines = lines[j:i+20]  # Include enough lines for the GameObject definition
                    break
        elif in_target:
            result_lines.append(line)
            # Check if we've reached the next GameObject or component that belongs to something else
            if line.startswith('--- !u!') and not any(f'fileID: {gameobject_id}' in lines[i+k] for k in range(1, min(5, len(lines)-i))):
                # We've hit the next object that doesn't belong to our target
                result_lines.pop()  # Remove the line we just added
                break
    
    return '\n'.join(result_lines) if result_lines else None

# Get both scene files
print("Reading main scene...")
main_scene = get_file_from_git('HEAD', 'Assets/Scenes/SampleScene.unity')

print("Reading Sapuni scene...")
sapuni_scene = get_file_from_git('Sapuni', 'Assets/Scenes/SampleScene.unity')

# Find all AudioSource components and BackgroundMusic GameObject in Sapuni
audio_pattern = r'--- !u!82 &\d+\nAudioSource:.*?(?=\n--- !u!|\Z)'
background_music_pattern = r'--- !u!1 &\d+\n.*?m_Name: BackgroundMusic.*?(?=\n--- !u!1|\Z)'

# Extract BackgroundMusic GameObject and all related components
audio_objects = []

# Find BackgroundMusic
bm_match = re.search(background_music_pattern, sapuni_scene, re.DOTALL)
if bm_match:
    audio_objects.append(bm_match.group(0))
    print(f"Found BackgroundMusic GameObject")

# Find all AudioSource components (they may be in separate sections)
for match in re.finditer(audio_pattern, sapuni_scene, re.DOTALL):
    # Check if this AudioSource is not already included
    if match.group(0) not in '\n'.join(audio_objects):
        audio_objects.append(match.group(0))
        print(f"Found AudioSource component")

# Now we need to insert these into the main scene
# Find where to insert (before the last few lines which are metadata)
# Unity scenes end with metadata - we want to insert before that

# Split the main scene into content and metadata
main_lines = main_scene.split('\n')

# Find the last GameObject (the one with highest ID usually)
last_gameobject_idx = -1
for i in range(len(main_lines)-1, -1, -1):
    if main_lines[i].startswith('--- !u!'):
        last_gameobject_idx = i
        break

print(f"Will insert audio objects before line {last_gameobject_idx}")

# Find all AudioSource component IDs from Sapuni to also extract those
audio_components = []
for match in re.finditer(r'--- !u!82 &(\d+)\nAudioSource:', sapuni_scene):
    comp_id = match.group(1)
    # Find the complete component definition
    start = match.start()
    # Find the next --- marker
    next_marker = sapuni_scene.find('\n---', start + 1)
    if next_marker == -1:
        next_marker = len(sapuni_scene)
    audio_components.append(sapuni_scene[start:next_marker])

print(f"Found {len(audio_components)} audio components to add")

# Rebuild the main scene with audio components
# Extract everything before the last GameObject section
if last_gameobject_idx > 0:
    new_content = '\n'.join(main_lines[:last_gameobject_idx])
    
    # Add all audio objects
    for obj in audio_components:
        new_content += '\n' + obj.lstrip()
    
    # Add the rest of the main scene
    new_content += '\n' + '\n'.join(main_lines[last_gameobject_idx:])
    
    # Write the merged scene
    with open('Assets/Scenes/SampleScene.unity', 'w') as f:
        f.write(new_content)
    
    print("Successfully merged audio into main scene!")
else:
    print("Could not find insertion point")
