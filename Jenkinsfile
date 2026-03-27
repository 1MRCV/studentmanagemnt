properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Select environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'DEPLOY = Build (DEV) or Deploy artifact (PROD). ROLLBACK = redeploy old build.'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch (only for DEV DEPLOY)'
    ),

    [$class: 'CascadeChoiceParameter',
      name: 'ARTIFACT_BUILD',
      description: 'Select build (only for PROD or rollback)',
      choiceType: 'PT_SINGLE_SELECT',
      filterable: true,
      script: [
        $class: 'GroovyScript',
        script: [
          $class: 'SecureGroovyScript',
          sandbox: false,
          script: '''
            import jenkins.model.Jenkins

            def job = Jenkins.instance.getItemByFullName("Test")
            if (job == null) return ["ERROR: Job not found"]

            def list = ["-- Select Build --"]

            job.getBuilds().each { build ->
              if (build.getResult()?.toString() == "SUCCESS") {
                def date = new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm").format(build.getTime())
                list << "Build_${build.getNumber()} | ${date}"
              }
            }

            return list
          '''
        ]
      ]
    ]

  ])
])

pipeline {

  agent { label 'windows-agent' }

  options {
    skipDefaultCheckout(true)
  }

  environment {
    GIT_REPO = 'https://github.com/1MRCV/studentmanagemnt.git'

    DEV_ARTIFACT_STORAGE  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_STORAGE = 'C:\\jenkins-artifacts\\PROD'

    DEV_IIS_PATH  = 'C:\\inetpub\\dev'
    PROD_IIS_PATH = 'C:\\inetpub\\prod'

    DEV_APP_POOL  = 'student-dev-pool'
    PROD_APP_POOL = 'student-prod-pool'

    DEV_SITE = 'student-dev'
    PROD_SITE = 'student-prod'

    DEV_PORT = '8081'
    PROD_PORT = '8082'

    PUBLISH_FOLDER = 'publish'
  }

  stages {

    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd HH:mm')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${params.ACTION} | ${date}"
        }
      }
    }

    stage('Clean Workspace') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        deleteDir()
      }
    }

    stage('Checkout Code') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])
      }
    }

    stage('Restore') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet restore'
      }
    }

    stage('Build') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet build --configuration Release'
      }
    }

    stage('Publish') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'
      }
    }

    stage('Create Artifact') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION == 'DEPLOY' }
        }
      }
      steps {
        powershell '''
          $build = $env:BUILD_NUMBER
          $date  = Get-Date -Format "yyyy-MM-dd"
          $time  = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

          $storage = $env:DEV_ARTIFACT_STORAGE
          if (!(Test-Path $storage)) { New-Item -ItemType Directory -Path $storage }

          $folder = "$storage\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force

          Compress-Archive -Path "publish\\*" -DestinationPath "$folder\\artifact.zip" -Force

          $commit = git rev-parse --short HEAD
          $branch = git rev-parse --abbrev-ref HEAD

@"
Build   : $build
Branch  : $branch
Commit  : $commit
Date    : $time
"@ | Out-File "$folder\\build-info.txt"
        '''
      }
    }

    stage('Promote to PROD') {
      when {
        expression { params.ENVIRONMENT == 'PRODUCTION' }
      }
      steps {
        powershell '''
          $selected = "${params.ARTIFACT_BUILD}"
          $buildNum = $selected.Split("|")[0].Replace("Build_","").Trim()

          $dev  = $env:DEV_ARTIFACT_STORAGE
          $prod = $env:PROD_ARTIFACT_STORAGE

          $folder = Get-ChildItem $dev -Directory | Where-Object { $_.Name -like "build_${buildNum}_*" } | Select-Object -First 1

          if ($null -eq $folder) { throw "Build not found" }

          if (!(Test-Path $prod)) { New-Item -ItemType Directory -Path $prod }

          Copy-Item $folder.FullName -Destination $prod -Recurse -Force
        '''
      }
    }

    stage('Deploy to IIS') {
      steps {
        powershell '''
          Import-Module WebAdministration

          $envName = "${params.ENVIRONMENT}"

          if ($envName -eq "DEV" -and "${params.ACTION}" -eq "DEPLOY") {
            $buildNum = $env:BUILD_NUMBER
            $storage = $env:DEV_ARTIFACT_STORAGE
          } else {
            $selected = "${params.ARTIFACT_BUILD}"
            $buildNum = $selected.Split("|")[0].Replace("Build_","").Trim()

            if ($envName -eq "DEV") {
              $storage = $env:DEV_ARTIFACT_STORAGE
            } else {
              $storage = $env:PROD_ARTIFACT_STORAGE
            }
          }

          $folder = Get-ChildItem $storage -Directory |
                    Where-Object { $_.Name -like "build_${buildNum}_*" } |
                    Select-Object -First 1

          if ($null -eq $folder) { throw "Artifact not found" }

          $zip = "$($folder.FullName)\\artifact.zip"

          if ($envName -eq "DEV") {
            $path = $env:DEV_IIS_PATH
            $pool = $env:DEV_APP_POOL
            $site = $env:DEV_SITE
            $port = $env:DEV_PORT
          } else {
            $path = $env:PROD_IIS_PATH
            $pool = $env:PROD_APP_POOL
            $site = $env:PROD_SITE
            $port = $env:PROD_PORT
          }

          if (!(Test-Path "IIS:\\AppPools\\$pool")) { New-WebAppPool -Name $pool }

          if (!(Test-Path "IIS:\\Sites\\$site")) {
            New-Item -ItemType Directory -Path $path -Force
            New-Website -Name $site -Port $port -PhysicalPath $path -ApplicationPool $pool
          }

          Stop-WebAppPool $pool -ErrorAction SilentlyContinue

          Remove-Item "$path\\*" -Recurse -Force -ErrorAction SilentlyContinue
          Expand-Archive $zip -DestinationPath $path -Force

          Start-WebAppPool $pool
          Start-Website $site

          Write-Host "Deployment Completed"
        '''
      }
    }

    stage('Verify') {
      steps {
        powershell '''
          Import-Module WebAdministration

          $pool = if ("${params.ENVIRONMENT}" -eq "DEV") { $env:DEV_APP_POOL } else { $env:PROD_APP_POOL }

          $state = (Get-WebAppPoolState -Name $pool).Value
          Write-Host "App Pool: $state"

          if ($state -ne "Started") {
            throw "Deployment failed"
          }
        '''
      }
    }

  }

  post {
    success {
      echo "SUCCESS: Build ${BUILD_NUMBER}"
    }
    failure {
      echo "FAILED: Check logs"
    }
  }
}
