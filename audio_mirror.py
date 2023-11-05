import os
import time
import shutil
import unicodedata

sanitization_count = 0

# Sanitize a file name
def sanitize_filename(filename):

    # To ensure global variable is referenced
    global sanitization_count

    # Remove any non-ASCII characters and replace wide characters with their closest equivalent
    sanitized_filename = ''.join((c if unicodedata.category(c)[0] not in ('C', 'M', 'Z') else ' ') for c in filename)
    sanitized_filename = unicodedata.normalize('NFKD', sanitized_filename).encode('ASCII', 'ignore').decode('ASCII')

    # If change made
    if filename != sanitized_filename:

        # Increment count
        sanitization_count += 1

        # Print the original filename
        # print("Sanitized:", sanitized_filename)

    return sanitized_filename


# Create folders
def create_directory_structure(source_folder, dest_folder):
    for root, dirs, files in os.walk(source_folder):
        for directory in dirs:
            directory_path = os.path.join(root, directory)
            relative_path = os.path.relpath(directory_path, source_folder)
            new_directory_path = os.path.join(dest_folder, relative_path)
            os.makedirs(new_directory_path, exist_ok=True)


# Create files
def mirror_mp3_files(source_folder, dest_folder):

    file_count = 0
    non_mp3_files = []

    for root, dirs, files in os.walk(source_folder):
        for file in files:

            # If file is an MP3 file
            if file.lower().endswith(".mp3"):
                file_count += 1

                # Sanitize the file name
                sanitized_filename = sanitize_filename(file)

                # Get file path
                file_path = os.path.join(root, sanitized_filename)
                relative_path = os.path.relpath(file_path, source_folder)
                new_file_path = os.path.join(dest_folder, relative_path)
                new_file_path = os.path.splitext(new_file_path)[0] + ".txt"

                # Create an empty .txt file if it doesn't exist already
                if not os.path.exists(new_file_path):
                    with open(new_file_path, 'w') as txt_file:
                        pass

                # Rename the .txt file with the sanitized name using shutil.move
                sanitized_txt_file_path = os.path.join(dest_folder, relative_path)
                sanitized_txt_file_path = os.path.splitext(sanitized_txt_file_path)[0] + ".txt"
                shutil.move(new_file_path, sanitized_txt_file_path)

            else:
                non_mp3_files.append(file)

    return file_count, non_mp3_files


# Create audio mirror
def create_mirror(root_folder, destination_folder):

    # Remove the destination folder if it exists
    if os.path.exists(destination_folder):
        shutil.rmtree(destination_folder)  

    create_directory_structure(root_folder, destination_folder)
    file_count, non_mp3_files = mirror_mp3_files(root_folder, destination_folder)

    return file_count, non_mp3_files


# MAIN
if __name__ == "__main__":

    # Start msg 
    print("\n### Audio Mirror ###")


    ### START ALGORITHM
    start_time = time.time()
    
    # Audio folder path
    folder_path = r'C:\Users\David\Audio'

    # Place mirror in folder next to script
    script_directory = os.path.dirname(os.path.abspath(__file__))
    destination_path = os.path.join(script_directory, "AUDIO_MIRROR")

    # Create mirror
    file_count, non_mp3_files = create_mirror(folder_path, destination_path)

    # Calculate execution time
    execution_time = time.time() - start_time


    ### PRINT INFO

    # Print file count
    print(f"\nMP3 file count: {file_count}")

    # Print sanitization count
    print(f"\nMP3 filenames sanitized: {sanitization_count}")

    # Print non MP3 files found
    print("\nNon-MP3 files found:")
    for file in non_mp3_files:
        print("   " + file)

    # Print time taken
    print(f"\nTime taken: {execution_time:.2f} seconds")

    # Finish msg
    print("\nFinished!\n")