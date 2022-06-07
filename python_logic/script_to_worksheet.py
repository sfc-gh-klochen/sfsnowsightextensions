import os
import argparse
from utils import *

if __name__ == '__main__':
    # ex file: python3 ./python_logic/script_to_worksheet.py --folder_or_filename /path/to/my_script.sql --role MYROLE --warehouse MYWAREHOUSE --database MYDATABASE --schema MYSCHEMA --account MYACCOUNT --out_folder path/to/my_out_dir
    # ex dir: python3 ./python_logic/script_to_worksheet.py --folder_or_filename /path/to/my/dir --role MYROLE --warehouse MYWAREHOUSE --database MYDATABASE --schema MYSCHEMA --account MYACCOUNT --out_folder path/to/my_out_dir
    parser = argparse.ArgumentParser(description='Read a one or many .sql scripts and turn them into worksheets')
    parser.add_argument('--folder_or_filename', '-f', type=str, help='relative path to folder/directory or file name', default='.')
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

        for file in [x for x in files if '.sql' in x.lower()]:
            script_to_worksheet(
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
        script_to_worksheet(
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