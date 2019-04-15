# Hack: don't want to commit the virtual env, so i need to pip install all the requirements before everything else
# to generate a new list of required packages run pip freeze > requirements.txt
import os
import subprocess

from venv_lib import activate_venv, deactivate_venv

import sys
import argparse
import zipfile
import glob
args = None

def main():
    import requests
    try:
        header = {"PRIVATE-TOKEN": os.environ['GITLAB_CI_TOKEN']}
        project_id = "burst%2Fburst"
        url = "https://gitlab.cds.internal.unity3d.com/api/v4/projects/{0}/jobs/{1}/artifacts".format(project_id, args.gitlab_job_id)
        result = requests.get(url, headers = header).content

        with open("burst.zip",'wb') as burst_build:
           burst_build.write(result)

        with zipfile.ZipFile("burst.zip", "r") as zip_ref:
           zip_ref.extractall(os.getcwd())

        print subprocess.check_output(['unity-downloader-cli', '-c', 'editor', '-c', args.platform , '-b','trunk', '-p', 'editor', '--wait'])

        #get_unity_launcher_script = "get_unity_launcher.py"
        #print subprocess.check_output(['python', get_unity_launcher_script])
        #if args.platform == "android":
        #    print subprocess.check_output(['/usr/local/bin/update_android_sdk.sh'])
        print subprocess.check_output(['./editor/Unity.app/Contents/MacOS/Unity',
                                       '-projectpath', os.path.join(os.getcwd(), 'build', 'Unity.Burst.Tests'),
                                       '-batchmode', '-silentcrashes',
                                       '-logfile', 'editor.log',
                                       '-runEditorTests', '-testresults', 'results.xml', '-testPlatform', args.platform])

    except subprocess.CalledProcessError as e:
        print e.output
        raise e
    except Exception as e:
        print e.message
        raise e
    finally:
        artifacts = [f for f in os.listdir('.') if f.endswith(".log") or f.endswith(".xml")]

        with zipfile.ZipFile("artifacts.zip", "w") as zip_ref:
            for f in artifacts:
                zip_ref.write(f)


def parse_args():
    global args
    parser = argparse.ArgumentParser(
        description='.')
    parser.add_argument('--platform', help='Specify the mobile target.')
    parser.add_argument('--gitlab_job_id', help='The id of the gitlab ci job.')

    args = parser.parse_args()

if __name__ == '__main__':
    activate_venv()
    try:
        parse_args()
        main()
    finally:
        deactivate_venv()