import os
import argparse
from utils import change_dashboard_configuration

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Change variables for dashboard json.')
    parser.add_argument('--folder_or_filename', '-f', type=str, help='relative path to folder/directory name', default='.')
    parser.add_argument('--role', '-r', type=str, help='role name')
    parser.add_argument('--warehouse', '-w', type=str, help='warehouse name')
    parser.add_argument('--database', '-d', type=str, help='database name', default='')
    parser.add_argument('--schema', '-s', type=str, help='schema name', default='')
    parser.add_argument('--account', '-a', type=str, help='account for filenaming', default='')
    parser.add_argument('--overwrite', '-ow', type=bool, help='overwrite files where they are', default=False)
    parser.add_argument('--out_folder', '-of', type=str, help='rename the directory you wish to be output to', default='')
    args = parser.parse_args()
    args = vars(args)

    if os.path.isdir(args['folder_or_filename']):
        files = os.listdir(args['folder_or_filename'])

        for file in [x for x in files if '.json' in x.lower()]:
            change_dashboard_configuration(
                folder=args['folder_or_filename'],
                filename=file,
                role=args['role'],
                warehouse=args['warehouse'],
                database=args['database'],
                schema=args['schema'],
                account=args['account'],
                overwrite=args['overwrite'],
                out_folder=args['out_folder']
            )
            
    else:
        foldername_parts = args['folder_or_filename'].split('/')
        file = foldername_parts[-1]
        del foldername_parts[-1]
        folder = '/'.join(foldername_parts)
        change_dashboard_configuration(
            folder=folder,
            filename=file,
            role=args['role'],
            warehouse=args['warehouse'],
            database=args['database'],
            schema=args['schema'],
            account=args['account'],
            overwrite=args['overwrite'],
            out_folder=args['out_folder']
        )