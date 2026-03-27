pipeline {
    agent { label 'windows-agent' }

    parameters {
        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV', 'PROD'],
            description: 'Select environment'
        )

        string(
            name: 'ARTIFACT_BUILD',
            defaultValue: '',
            description: 'Required only for PROD (e.g. Build_21 | 2026-03-27)'
        )
    }

    environment {
        DEV_ARTIFACT_STORAGE  = "C:\\jenkins-artifacts\\DEV"
        PROD_ARTIFACT_STORAGE = "C:\\jenkins-artifacts\\PROD"
    }

    stages {

        stage('Checkout Code') {
            when { expression { params.ENVIRONMENT == 'DEV' } }
            steps {
                deleteDir()
                checkout scm
            }
        }

        stage('Build + Publish (DEV only)') {
            when { expression { params.ENVIRONMENT == 'DEV' } }
            steps {
                powershell '''
                dotnet restore
                dotnet build --configuration Release
                dotnet publish -c Release -o publish
                '''
            }
        }

        stage('Create Artifact (DEV only)') {
            when { expression { params.ENVIRONMENT == 'DEV' } }
            steps {
                powershell '''
                $date = Get-Date -Format "yyyy-MM-dd"
                $buildNum = $env:BUILD_NUMBER

                $artifactPath = "$env:DEV_ARTIFACT_STORAGE\\build_${buildNum}_$date"

                New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
                Copy-Item "publish\\*" $artifactPath -Recurse -Force

                Write-Host "Artifact created: $artifactPath"
                '''
            }
        }

        stage('Deploy') {
            steps {
                powershell '''

                if ("${params.ENVIRONMENT}" -eq "DEV") {
                    # Direct deploy from publish
                    $source = "publish"
                }
                else {
                    # PROD → use selected artifact
                    if ("${params.ARTIFACT_BUILD}" -eq "") {
                        throw "ARTIFACT_BUILD is required for PROD"
                    }

                    $buildNum = "${params.ARTIFACT_BUILD}".Split("|")[0].Replace("Build_","").Trim()

                    $folder = Get-ChildItem "$env:DEV_ARTIFACT_STORAGE" -Directory |
                              Where-Object { $_.Name -like "build_${buildNum}_*" } |
                              Select-Object -First 1

                    if ($null -eq $folder) {
                        throw "Artifact not found for build $buildNum"
                    }

                    $source = $folder.FullName
                }

                $destination = "C:\\inetpub\\wwwroot\\StudentPortal"

                if (!(Test-Path $source)) {
                    throw "Source not found: $source"
                }

                # Stop IIS
                iisreset /stop

                # Clean
                Remove-Item "$destination\\*" -Recurse -Force -ErrorAction SilentlyContinue

                # Copy
                Copy-Item "$source\\*" $destination -Recurse -Force

                # Start IIS
                iisreset /start

                Write-Host "Deployment SUCCESS from $source"
                '''
            }
        }

        stage('Verify') {
            steps {
                echo "Deployment completed!"
            }
        }
    }

    post {
        failure {
            echo "FAILED"
        }
    }
}
