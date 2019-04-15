import argparse
import json
import os
import sys

import requests
import katana_lib

args = None

platforms = ['iOS', 'Android']

def main():
    global args
    if args.debug:
        os.environ['CI_COMMIT_REF_NAME'] = "run_katana_builds"
        os.environ['CI_PIPELINE_ID'] = "20100"
        os.environ['CI_COMMIT_SHA'] = ""

    print os.environ['CI_COMMIT_REF_NAME']
    print os.environ['CI_PIPELINE_ID']

    header = {"PRIVATE-TOKEN": os.environ['CI_ACCESS_TOKEN']}

    project_id = "burst%2Fburst"
    pipeline_id = os.environ['CI_PIPELINE_ID']

    url = "https://gitlab.cds.internal.unity3d.com/api/v4/projects/{0}/pipelines/{1}/jobs/".format(project_id, pipeline_id)
    r = json.loads(requests.get(url, headers = header).content)

    id = next(item['id'] for item in r if item['name'] == "build_burst:{0}".format(args.os_host))

    properties = {
        "force_chain_rebuild": "true",
        "force_rebuild": "true",
        "priority": "50",
        "gitlab_job_id": id }

    project = 'proj119-Burst {0}'.format(args.mobile_platform)
    build_number = katana_lib.start_katana_build(project, properties)

    if args.debug:
        os.environ['CI_COMMIT_REF_NAME'] = ""
        os.environ['CI_PIPELINE_ID'] = ""

    while not katana_lib.has_katana_finished(build_number, project):
        pass

    # get test results
    katana_lib.process_running_builds(build_number, project)
    #parse test results

    #return exit code


def parse_args():
    global args
    parser = argparse.ArgumentParser(
        description='')
    parser.add_argument('--os_host', help='OS host of the build.', required=True)
    parser.add_argument('--mobile_platform', help='Mobile platform.', required=True)
    parser.add_argument('--debug', default=False, action='store_true')
    args = parser.parse_args()
    if args.mobile_platform not in platforms:
        raise Exception ("Unknown platform {0}".format(args.mobile_platform))

if __name__ == '__main__':
    parse_args()
    main()
