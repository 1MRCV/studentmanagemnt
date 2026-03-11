pipeline {
    agent { label 'windows-agent' }

    parameters {

        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV','PRODUCTION'],
            description: 'Select environment to deploy'
        )

        string(
            name: 'DEPLOY_BUILD',
            defaultValue: '',
            description: 'Artifact version to deploy (leave empty for latest)'
        )

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        PUBLISH_FOLDER = "publish"

        ARTIFACT_STORAGE = "C:\\jenkins-artifacts"

        DEV_PATH = "C:\\inetpub\\dev"
        PROD_PATH = "C:\\inetpub\\prod"

        DEV_SITE = "student-dev"
        PROD_SITE = "student-prod"

        DEV_POOL = "student-dev-pool"
        PROD_POOL = "student-prod-pool"

        DEV_PORT = "8081"
        PROD_PORT = "8082"
    }

    stages {

        stage('Clean Workspace') {
            steps { deleteDir() }
        }

        stage('Checkout Code') {
            steps {
                git branch: "${params.BRANCH}",
                    url: "${env.GIT_REPO}"
            }
        }

        stage('Restore Dependencies') {
            steps {
                powershell 'dotnet restore'
            }
        }

        stage('Build Application') {
            steps {
                powershell 'dotnet build --configuration Release'
            }
        }

        stage('Publish Website') {
            steps {
                powershell 'dotnet publish -c Release -o $env:PUBLISH_FOLDER'
            }
        }

        stage('Create Artifact Version') {
            steps {
                powershell '''

                $storage = "$env:ARTIFACT_STORAGE"
                $zip = "$storage\\build_$env:BUILD_NUMBER.zip"

                if (!(Test-Path $storage)) {
                    New-Item -ItemType Directory -Path $storage
                }

                Compress-Archive `
                    -Path "$env:PUBLISH_FOLDER\\*" `
                    -DestinationPath $zip `
                    -Force

                Write-Host "Artifact stored: $zip"
                '''
            }
        }

        stage('Deploy') {
            steps {
                powershell '''

                Import-Module WebAdministration

                $envName = "$env:ENVIRONMENT"
                $buildToDeploy = "$env:DEPLOY_BUILD"

                if ([string]::IsNullOrEmpty($buildToDeploy)) {
                    $buildToDeploy = "$env:BUILD_NUMBER"
                }

                $zipPath = "$env:ARTIFACT_STORAGE\\build_$buildToDeploy.zip"

                if (!(Test-Path $zipPath)) {
                    throw "Artifact not found: $zipPath"
                }

                if ($envName -eq "DEV") {

                    $siteName = "$env:DEV_SITE"
                    $pool = "$env:DEV_POOL"
                    $path = "$env:DEV_PATH"
                    $port = "$env:DEV_PORT"

                } else {

                    $siteName = "$env:PROD_SITE"
                    $pool = "$env:PROD_POOL"
                    $path = "$env:PROD_PATH"
                    $port = "$env:PROD_PORT"

                }

                Write-Host "Deploying Build $buildToDeploy to $envName"

                if (!(Test-Path "IIS:\\AppPools\\$pool")) {
                    New-WebAppPool -Name $pool
                }

                if (!(Test-Path $path)) {
                    New-Item -ItemType Directory -Path $path
                }

                if (!(Test-Path "IIS:\\Sites\\$siteName")) {

                    New-Website `
                        -Name $siteName `
                        -Port $port `
                        -PhysicalPath $path `
                        -ApplicationPool $pool

                }

                $state = (Get-WebAppPoolState -Name $pool).Value

                if ($state -eq "Started") {
                    Stop-WebAppPool $pool
                    Start-Sleep -Seconds 2
                }

                Remove-Item "$path\\*" -Recurse -Force -ErrorAction SilentlyContinue

                Expand-Archive `
                    -Path $zipPath `
                    -DestinationPath $path `
                    -Force

                $versionFile = "$path\\version.txt"

                "Environment: $envName" | Out-File $versionFile
                "Build: $buildToDeploy" | Out-File $versionFile -Append
                "Date: $(Get-Date)" | Out-File $versionFile -Append

                Start-WebAppPool $pool
                Start-Website $siteName

                Write-Host "Deployment completed"

                '''
            }
        }

        stage('Verify Deployment') {
            steps {
                powershell '''

                Import-Module WebAdministration

                if ($env:ENVIRONMENT -eq "DEV") {
                    $pool="$env:DEV_POOL"
                } else {
                    $pool="$env:PROD_POOL"
                }

                $state = (Get-WebAppPoolState -Name $pool).Value

                Write-Host "App Pool State: $state"

                if ($state -ne "Started") {
                    throw "Deployment failed"
                }

                '''
            }
        }
    }

    post {

        success {
            echo "Deployment successful"
        }

        failure {
            echo "Deployment failed"
        }

    }
}
